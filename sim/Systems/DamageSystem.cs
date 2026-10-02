using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

// Auto-attacks pay their period on the swing; windup is inside that period.
public class DamageSystem : ISystem {
  private readonly UnitLookup.Index _unitIdIndex = new();

  public void Update(ref Frame frame) {
    _unitIdIndex.Rebuild(ref frame);

    // Resolve pending swings even after their intent is gone.
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

    // One id covers the windup and hit.
    var attackHitId = DamageController.NextHitId(ref frame);
    var cooldownTicks = BurstAttacksController.NextCooldownTicks(ref frame, attacker,
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

    // Zero-windup attacks land on the swing tick.
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

    // Consume the damage modifier only on a hit.
    var attackDamage = EmpoweredAttackController.Consume(ref frame, attacker, target, attackHitId,
      GetAttackDamage(ref frame, attacker));

    var damage = DamageController.ApplyDamage(ref frame, attacker, target, attackDamage,
      DamageType.Physical, canCrit: true, attackHitId: attackHitId);

    ClearSwing(ref frame, attacker);

    LogDamageState(ref frame, attacker, targetUnitId,
      $"damage={damage} health={healthBefore}->{frame.GetReadOnly<Health>(target).Current} " +
      $"cooldown={frame.GetReadOnly<Combat>(attacker).CooldownRemainingTicks}");
  }

  // Cooldown is paid on the swing, even if the hit misses.
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

  // Attackers without stats deal no basic-attack damage.
  private static FP64 GetAttackDamage(ref Frame frame, EntityRef attacker) {
    return frame.Has<Stats>(attacker)
      ? frame.GetReadOnly<Stats>(attacker).AttackDamage
      : FP64.Zero;
  }

  private static void LogDamageState(ref Frame frame, EntityRef attacker, int targetUnitId, string state) {
    SimLog.Debug(ref frame,
      $"[Combat] event=attack_state tick={frame.Tick} sourceUnitId={UnitLookup.GetUnitId(ref frame, attacker)} " +
      $"targetUnitId={targetUnitId} state={state}");
  }
}
