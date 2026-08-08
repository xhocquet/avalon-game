using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

// The one place health goes up, mirroring DamageApplication on the way down. Heals, regen, respawns,
// and level-up top-ups all clamp against Stats.MaxHealth here rather than each deriving the rule.
public static class HealthApplication {
  // Returns HP actually restored. A unit at 0 stays there: it is either awaiting a respawn or about
  // to be destroyed, and topping it up would read as alive to everything that checks Current.
  public static int ApplyHeal(ref Frame frame, EntityRef target, int amount) {
    if (amount <= 0 || !frame.Has<Health>(target))
      return 0;

    ref var health = ref frame.Get<Health>(target);
    if (!health.IsAlive)
      return 0;

    var headroom = GetMaxHealth(ref frame, target) - health.Current;
    if (headroom <= 0)
      return 0;

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
  // killing — a death here would carry no killer and pay no XP.
  public static void GrantMaxHealth(ref Frame frame, EntityRef target, int amount) {
    if (amount == 0 || !frame.Has<StatsComponent>(target))
      return;

    frame.Get<StatsComponent>(target).Add(StatType.MaxHealth, FP64.FromInt(amount));

    if (amount > 0) {
      ApplyHeal(ref frame, target, amount);
      return;
    }

    if (!frame.Has<Health>(target))
      return;

    ref var health = ref frame.Get<Health>(target);
    var max = GetMaxHealth(ref frame, target);
    if (health.IsAlive && health.Current > max)
      health.Current = max < 1 ? 1 : max;
  }

  // A unit with no StatsComponent has no pool to fill, so healing it is a no-op rather than unbounded.
  public static int GetMaxHealth(ref Frame frame, EntityRef target) =>
    frame.Has<StatsComponent>(target) ? frame.GetReadOnly<StatsComponent>(target).MaxHealth : 0;
}
