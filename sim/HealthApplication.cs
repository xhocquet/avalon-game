using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

// The one place health goes up, mirroring DamageApplication on the way down. Heals, regen, respawns,
// and level-up top-ups all clamp against Stats.MaxHealth here rather than each deriving the rule.
public static class HealthApplication {
  // Returns HP actually restored. A unit at 0 stays there: it is either awaiting a respawn or about
  // to be destroyed, and topping it up would read as alive to everything that checks Current.
  public static FP64 ApplyHeal(ref Frame frame, EntityRef target, FP64 amount) {
    if (amount <= FP64.Zero || !frame.Has<Health>(target))
      return FP64.Zero;

    ref var health = ref frame.Get<Health>(target);
    if (!health.IsAlive)
      return FP64.Zero;

    var headroom = GetMaxHealth(ref frame, target) - health.Current;
    if (headroom <= FP64.Zero)
      return FP64.Zero;

    var healed = amount < headroom ? amount : headroom;
    health.Current += healed;
    return healed;
  }

  // Skips the alive check on purpose: a respawn is what brings a unit back from zero.
  public static void RestoreToFull(ref Frame frame, EntityRef target) {
    if (frame.Has<Health>(target))
      frame.Get<Health>(target).Current = GetMaxHealth(ref frame, target);
  }

  // Grows the pool and hands the same amount over as HP, so a bigger max never reads as a heal debt.
  // A negative amount shrinks the pool and pulls Current down with it, stopping at 1 rather than
  // killing - a death here would carry no killer and pay no XP.
  public static void GrantMaxHealth(ref Frame frame, EntityRef target, FP64 amount) {
    if (amount == FP64.Zero || !frame.Has<StatsComponent>(target))
      return;

    frame.Get<StatsComponent>(target).Add(StatType.MaxHealth, amount);

    if (amount > FP64.Zero) {
      ApplyHeal(ref frame, target, amount);
      return;
    }

    if (!frame.Has<Health>(target))
      return;

    ref var health = ref frame.Get<Health>(target);
    var max = GetMaxHealth(ref frame, target);
    if (health.IsAlive && health.Current > max)
      health.Current = max < FP64.One ? FP64.One : max;
  }

  // A unit with no StatsComponent has no pool to fill, so healing it is a no-op rather than unbounded.
  public static FP64 GetMaxHealth(ref Frame frame, EntityRef target) =>
    frame.Has<StatsComponent>(target) ? frame.GetReadOnly<StatsComponent>(target).MaxHealth : FP64.Zero;
}
