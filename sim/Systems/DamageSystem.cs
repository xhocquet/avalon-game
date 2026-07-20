using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Models;
using xpTURN.Klotho.Core;
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

      var damage = StatsSystem.CalculateDamage(ref frame, attacker, target);

      ref var health = ref frame.Get<Health>(target);
      var healthBefore = health.Current;
      health.Current -= damage;
      if (health.Current < 0)
        health.Current = 0;

      combat.CooldownRemainingTicks = combat.AttackCooldownTicks;

      if (frame.EventRaiser != null) {
        var evt = EventPool.Get<AttackHitEvent>();
        evt.AttackerUnitId = TryGetUnitId(ref frame, attacker, out var srcId) ? srcId : 0;
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
    frame.Logger?.KDebug(
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
