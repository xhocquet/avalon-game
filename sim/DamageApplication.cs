using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

// The one place health goes down. Auto-attacks (DamageSystem) and skills both route through here so
// mitigation, the zero floor, kill credit, and the hit event can never drift apart between them.
public static class DamageApplication {
  // Returns the damage actually dealt after mitigation.
  public static int ApplyDamage(ref Frame frame, EntityRef source, EntityRef target, int amount) {
    var damage = Mitigate(ref frame, target, amount);
    var sourceUnitId = UnitLookup.GetUnitId(ref frame, source);

    ref var health = ref frame.Get<Health>(target);
    health.Current -= damage;
    if (health.Current < 0)
      health.Current = 0;

    // Load-bearing, not bookkeeping: DeathSystem and RespawnSystem read this to resolve the killer,
    // and ExperienceRewards.AwardForKill pays out against it. A kill that skips it awards nobody.
    health.LastDamagerUnitId = sourceUnitId;

    RaiseHitEvent(ref frame, source, target, sourceUnitId, damage);
    return damage;
  }

  // Defense mitigates by a fraction rather than a flat subtraction, so stacking it approaches but never
  // reaches immunity and low-damage attackers stay relevant.
  public static int Mitigate(ref Frame frame, EntityRef target, int damage) {
    if (damage <= 0 || !frame.Has<StatsComponent>(target))
      return damage;

    var defense = frame.GetReadOnly<StatsComponent>(target).Defense;
    if (defense <= 0)
      return damage;

    var mitigated = damage * 100 / (100 + defense);
    return mitigated < 1 ? 1 : mitigated; // Floor at 1 damage
  }

  private static void RaiseHitEvent(ref Frame frame, EntityRef source, EntityRef target,
    int sourceUnitId, int damage) {
    if (frame.EventRaiser == null)
      return;

    var evt = EventPool.Get<AttackHitEvent>();
    evt.AttackerUnitId = sourceUnitId;
    evt.TargetUnitId = UnitLookup.GetUnitId(ref frame, target);
    evt.Damage = damage;
    evt.AttackerPosition = frame.Has<TransformComponent>(source)
      ? frame.GetReadOnly<TransformComponent>(source).Position
      : default;
    evt.TargetPosition = frame.Has<TransformComponent>(target)
      ? frame.GetReadOnly<TransformComponent>(target).Position
      : default;
    frame.EventRaiser.RaiseEvent(evt);
  }
}
