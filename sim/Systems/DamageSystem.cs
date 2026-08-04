using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Logging;

namespace Meesles.Avalon;

public class DamageSystem : ISystem {
  public void Update(ref Frame frame) {
    var filter = frame.Filter<Combat, TeamComponent, AttackTargetUnitId>();
    while (filter.Next(out var attacker)) {
      ref var combat = ref frame.Get<Combat>(attacker);
      if (combat.CooldownRemainingTicks > 0) {
        LogCooldownBoundary(ref frame, attacker, combat);
        continue;
      }

      if (!combat.Target.IsValid)
        continue;

      var target = combat.Target;
      if (!CombatTargeting.IsHostileAndAlive(ref frame, attacker, target)) {
        LogDamageState(ref frame, attacker, combat.Target, "invalid_damage_target");
        combat.Target = default;
        continue;
      }

      var damage = Mitigate(ref frame, target, GetAttackDamage(ref frame, attacker));
      var attackerUnitId = UnitLookup.GetUnitId(ref frame, attacker);

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
        evt.TargetUnitId = UnitLookup.GetUnitId(ref frame, target);
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

  // Attackers without a StatsComponent block (nothing today, but structures/summons may skip it) deal nothing.
  private static int GetAttackDamage(ref Frame frame, EntityRef attacker) {
    return frame.Has<StatsComponent>(attacker) ? frame.GetReadOnly<StatsComponent>(attacker).AttackDamage : 0;
  }

  // Defense mitigates by a fraction rather than a flat subtraction, so stacking it approaches but never
  // reaches immunity and low-damage attackers stay relevant. Integer math throughout to keep it
  // deterministic; any landed hit floors at 1 so a high-defense target can never be unkillable.
  private static int Mitigate(ref Frame frame, EntityRef target, int damage) {
    if (damage <= 0 || !frame.Has<StatsComponent>(target))
      return damage;

    var defense = frame.GetReadOnly<StatsComponent>(target).Defense;
    if (defense <= 0)
      return damage;

    var mitigated = damage * 100 / (100 + defense);
    return mitigated < 1 ? 1 : mitigated;
  }

  // Combat.AttackCooldownTicks is the unit's base period; StatsComponent.AttackSpeed is the multiplier items
  // and skills move. Dividing here rather than storing a modified period means bonuses stay additive
  // on the rate (two +50% items give ×2, not ×2.25) and rounding happens once per attack instead of
  // compounding. Rounds to nearest tick, floors at 1 so no attack speed can fire twice in a tick.
  private static int GetCooldownTicks(ref Frame frame, EntityRef attacker, in Combat combat) {
    var attackSpeed = frame.Has<StatsComponent>(attacker) ? frame.GetReadOnly<StatsComponent>(attacker).AttackSpeed : FP64.One;
    if (attackSpeed <= FP64.Zero)
      return combat.AttackCooldownTicks;

    var half = FP64.One / FP64.FromInt(2);
    var ticks = (FP64.FromInt(combat.AttackCooldownTicks) / attackSpeed + half).ToInt();
    return ticks < 1 ? 1 : ticks;
  }

  private static void LogCooldownBoundary(ref Frame frame, EntityRef attacker, in Combat combat) {
    var cooldownStarted = combat.CooldownRemainingTicks == combat.AttackCooldownTicks - 1;
    var cooldownEnding = combat.CooldownRemainingTicks == 1;
    if (cooldownStarted || cooldownEnding)
      LogDamageState(ref frame, attacker, combat.Target,
        $"cooldown_blocked cooldown={combat.CooldownRemainingTicks}");
  }

  private static void LogDamageState(ref Frame frame, EntityRef attacker, EntityRef target, string state) {
    frame.Logger.KDebug(
      $"[Combat] DamageSystem tick={frame.Tick} sourceUnitId={UnitLookup.GetUnitId(ref frame, attacker)} " +
      $"targetUnitId={UnitLookup.GetUnitId(ref frame, target)} state={state}");
  }
}
