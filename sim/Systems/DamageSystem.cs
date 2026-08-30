using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Logging;

namespace Meesles.Avalon;

// Auto-attacks, in two phases. A swing starts when the attacker is engaged and off cooldown
// (AttackWindupStartedEvent), and lands AttackWindup seconds later (AttackHitEvent) - or is dropped
// if the target stopped being a legal one in between (AttackWindupCanceledEvent).
//
// The attack period is paid at the swing, not the hit, so windup is spent inside the period rather
// than added to it and a unit's attacks-per-second means what it is authored to mean. A swing that
// whiffs still costs it.
public class DamageSystem : ISystem {
  private readonly UnitLookup.Index _unitIdIndex = new();

  public void Update(ref Frame frame) {
    _unitIdIndex.Rebuild(ref frame);

    // Swings in flight resolve on the bare Combat filter, not the engaged one: AttackIntentSystem
    // drops AttackTargetUnitId the moment an order is spent, and a swing left out of that filter
    // would keep its WindupReleaseTick forever and never attack again.
    var swinging = frame.Filter<Combat>();
    while (swinging.Next(out var attacker)) {
      if (frame.GetReadOnly<Combat>(attacker).WindupReleaseTick != 0)
        ResolveSwing(ref frame, attacker);
    }

    var engaged = frame.Filter<Combat, Team, AttackTargetUnitId>();
    while (engaged.Next(out var attacker)) {
      ref readonly var combat = ref frame.GetReadOnly<Combat>(attacker);
      if (combat.WindupReleaseTick == 0 && combat.CooldownRemainingTicks == 0 && combat.TargetUnitId != 0)
        StartSwing(ref frame, attacker);
    }
  }

  private void StartSwing(ref Frame frame, EntityRef attacker) {
    var targetUnitId = frame.GetReadOnly<Combat>(attacker).TargetUnitId;
    if (!TryResolveTarget(ref frame, attacker, targetUnitId, out var target)) {
      LogDamageState(ref frame, attacker, targetUnitId, "invalid_damage_target");
      frame.Get<Combat>(attacker).TargetUnitId = 0;
      return;
    }

    // Taken up front so the whole swing - the windup event, anything that modifies the damage on the
    // way in, and the hit - reports itself under one id.
    var attackHitId = DamageApplication.NextHitId(ref frame);
    var cooldownTicks = AttackBursts.NextCooldownTicks(ref frame, attacker,
      CombatTiming.CooldownTicks(ref frame, attacker));
    var windupTicks = CombatTiming.WindupTicks(ref frame, attacker, cooldownTicks);

    ref var combat = ref frame.Get<Combat>(attacker);
    combat.CooldownRemainingTicks = cooldownTicks;
    combat.WindupAttackHitId = attackHitId;
    combat.WindupTargetUnitId = targetUnitId;
    combat.WindupReleaseTick = frame.Tick + windupTicks;

    AttackPhases.RaiseWindupStarted(ref frame, attacker, target, targetUnitId, attackHitId, windupTicks);
    LogDamageState(ref frame, attacker, targetUnitId,
      $"windup_started windupTicks={windupTicks} cooldown={combat.CooldownRemainingTicks}");

    // Nothing authored a windup, so the swing is the hit. Resolved here rather than a tick later so
    // a zero-windup unit keeps the cadence it had before windup existed.
    if (windupTicks == 0)
      ResolveSwing(ref frame, attacker);
  }

  private void ResolveSwing(ref Frame frame, EntityRef attacker) {
    ref readonly var pending = ref frame.GetReadOnly<Combat>(attacker);
    var attackHitId = pending.WindupAttackHitId;
    var targetUnitId = pending.WindupTargetUnitId;
    var releaseTick = pending.WindupReleaseTick;

    if (!TryResolveTarget(ref frame, attacker, targetUnitId, out var target) ||
        !CombatRange.IsWithinReach(ref frame, attacker, target, out _, out _)) {
      ClearSwing(ref frame, attacker);
      AttackPhases.RaiseWindupCanceled(ref frame, attacker, targetUnitId, attackHitId);
      LogDamageState(ref frame, attacker, targetUnitId, "windup_canceled");
      return;
    }

    if (frame.Tick < releaseTick)
      return;

    var healthBefore = frame.GetReadOnly<Health>(target).Current;

    // Spent at the hit, not the swing: the multiplier applies to damage, and a swing that whiffs
    // must not eat the charge the player is holding.
    var attackDamage = AttackProcs.Consume(ref frame, attacker, target, attackHitId,
      GetAttackDamage(ref frame, attacker));

    var damage = DamageApplication.ApplyDamage(ref frame, attacker, target, attackDamage,
      DamageType.Physical, canCrit: true, attackHitId: attackHitId);

    ClearSwing(ref frame, attacker);

    LogDamageState(ref frame, attacker, targetUnitId,
      $"damage={damage} health={healthBefore}->{frame.GetReadOnly<Health>(target).Current} " +
      $"cooldown={frame.GetReadOnly<Combat>(attacker).CooldownRemainingTicks}");
  }

  // Leaves CooldownRemainingTicks alone - it was paid at the swing and stands whether the hit landed.
  private static void ClearSwing(ref Frame frame, EntityRef attacker) {
    ref var combat = ref frame.Get<Combat>(attacker);
    combat.WindupReleaseTick = 0;
    combat.WindupAttackHitId = 0;
    combat.WindupTargetUnitId = 0;
  }

  private bool TryResolveTarget(ref Frame frame, EntityRef attacker, int targetUnitId,
    out EntityRef target) {
    return _unitIdIndex.TryGet(targetUnitId, out target) &&
           CombatTargeting.IsHostileAndAlive(ref frame, attacker, target);
  }

  // Attackers without a Stats block (nothing today, but structures/summons may skip it) deal nothing.
  private static FP64 GetAttackDamage(ref Frame frame, EntityRef attacker) {
    return frame.Has<Stats>(attacker)
      ? frame.GetReadOnly<Stats>(attacker).AttackDamage
      : FP64.Zero;
  }

  private static void LogDamageState(ref Frame frame, EntityRef attacker, int targetUnitId, string state) {
    frame.Logger.KDebug(
      $"[Combat] DamageSystem tick={frame.Tick} sourceUnitId={UnitLookup.GetUnitId(ref frame, attacker)} " +
      $"targetUnitId={targetUnitId} state={state}");
  }
}
