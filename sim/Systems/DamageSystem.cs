using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Logging;

namespace Meesles.Avalon;

public class DamageSystem : ISystem {
  public void Update(ref Frame frame) {
    var filter = frame.Filter<Combat, Team, AttackTargetUnitId>();
    while (filter.Next(out var attacker)) {
      ref var combat = ref frame.Get<Combat>(attacker);
      if (combat.CooldownRemainingTicks > 0) {
        LogCooldownBoundary(ref frame, attacker, combat);
        continue;
      }

      if (!combat.Target.IsValid)
        continue;

      if (!TryGetDamageTarget(ref frame, attacker, combat.Target, out var target)) {
        LogDamageState(ref frame, attacker, combat.Target, "invalid_damage_target");
        combat.Target = default;
        continue;
      }

      var damage = GetAttackDamage(ref frame, attacker);
      var attackerUnitId = TryGetUnitId(ref frame, attacker, out var srcId) ? srcId : 0;

      ref var health = ref frame.Get<Health>(target);
      var healthBefore = health.Current;
      health.Current -= damage;
      if (health.Current < 0)
        health.Current = 0;
      health.LastDamagerUnitId = attackerUnitId;

      combat.CooldownRemainingTicks = GetCooldownTicks(ref frame, attacker, in combat);

      if (frame.EventRaiser != null) {
        var evt = EventPool.Get<AttackHitEvent>();
        evt.AttackerUnitId = attackerUnitId;
        evt.TargetUnitId = TryGetUnitId(ref frame, target, out var tgtId) ? tgtId : 0;
        evt.Damage = damage;
        evt.AttackerPosition = frame.Has<TransformComponent>(attacker)
          ? frame.GetReadOnly<TransformComponent>(attacker).Position
          : default;
        evt.TargetPosition = frame.Has<TransformComponent>(target)
          ? frame.GetReadOnly<TransformComponent>(target).Position
          : default;
        frame.EventRaiser.RaiseEvent(evt);
      }

      LogDamageState(ref frame, attacker, target,
        $"damage={damage} health={healthBefore}->{health.Current} cooldown={combat.CooldownRemainingTicks}");
    }
  }

  // Attackers without a Stats block (nothing today, but structures/summons may skip it) deal nothing.
  private static int GetAttackDamage(ref Frame frame, EntityRef attacker) {
    return frame.Has<Stats>(attacker) ? frame.GetReadOnly<Stats>(attacker).AttackDamage : 0;
  }

  // Combat.AttackCooldownTicks is the unit's base period; Stats.AttackSpeed is the multiplier items
  // and skills move. Dividing here rather than storing a modified period means bonuses stay additive
  // on the rate (two +50% items give ×2, not ×2.25) and rounding happens once per attack instead of
  // compounding. Rounds to nearest tick, floors at 1 so no attack speed can fire twice in a tick.
  private static int GetCooldownTicks(ref Frame frame, EntityRef attacker, in Combat combat) {
    var attackSpeed = frame.Has<Stats>(attacker) ? frame.GetReadOnly<Stats>(attacker).AttackSpeed : FP64.One;
    if (attackSpeed <= FP64.Zero)
      return combat.AttackCooldownTicks;

    var half = FP64.One / FP64.FromInt(2);
    var ticks = (FP64.FromInt(combat.AttackCooldownTicks) / attackSpeed + half).ToInt();
    return ticks < 1 ? 1 : ticks;
  }

  private static bool TryGetDamageTarget(ref Frame frame, EntityRef attacker, EntityRef target,
    out EntityRef resolvedTarget) {
    resolvedTarget = target;
    if (!target.IsValid || !frame.Has<Health>(target) || !frame.Has<Team>(target))
      return false;

    ref readonly var health = ref frame.GetReadOnly<Health>(target);
    if (health.Current <= 0)
      return false;

    ref readonly var attackerTeam = ref frame.GetReadOnly<Team>(attacker);
    ref readonly var targetTeam = ref frame.GetReadOnly<Team>(target);
    return attackerTeam.TeamId != targetTeam.TeamId;
  }

  private static void LogCooldownBoundary(ref Frame frame, EntityRef attacker, in Combat combat) {
    var cooldownStarted = combat.CooldownRemainingTicks == combat.AttackCooldownTicks - 1;
    var cooldownEnding = combat.CooldownRemainingTicks == 1;
    if (cooldownStarted || cooldownEnding)
      LogDamageState(ref frame, attacker, combat.Target,
        $"cooldown_blocked cooldown={combat.CooldownRemainingTicks}");
  }

  private static void LogDamageState(ref Frame frame, EntityRef attacker, EntityRef target, string state) {
    var sourceUnitId = TryGetUnitId(ref frame, attacker, out var source) ? source : 0;
    var targetUnitId = target.IsValid && TryGetUnitId(ref frame, target, out var resolvedTarget) ? resolvedTarget : 0;
    frame.Logger.KDebug(
      $"[Combat] DamageSystem tick={frame.Tick} sourceUnitId={sourceUnitId} targetUnitId={targetUnitId} state={state}");
  }

  private static bool TryGetUnitId(ref Frame frame, EntityRef entity, out int unitId) {
    if (entity.IsValid && frame.Has<Unit>(entity)) {
      unitId = frame.GetReadOnly<Unit>(entity).UnitId;
      return true;
    }

    unitId = 0;
    return false;
  }
}
