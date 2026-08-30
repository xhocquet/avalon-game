using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

// The resource sibling of HealthApplication: the one place mana moves. Skill costs spend through
// TrySpend, ManaRestore effects refill through Restore, level-up grows the pool through GrantMaxMana,
// and every path clamps against Stats.MaxMana here rather than each deriving the rule.
public static class ManaApplication {
  // A unit with no StatsComponent, or one whose MaxMana is 0, has no pool - restores are a no-op and
  // spends fail rather than going negative.
  public static FP64 GetMaxMana(ref Frame frame, EntityRef target) =>
    frame.Has<StatsComponent>(target) ? frame.GetReadOnly<StatsComponent>(target).MaxMana : FP64.Zero;

  // Would TrySpend succeed right now? Read-only and allocation-free, for the cast gate the client
  // polls every frame. A cost of zero or less always affords.
  public static bool CanAfford(ref Frame frame, EntityRef caster, FP64 amount) =>
    amount <= FP64.Zero ||
    (frame.Has<Health>(caster) && frame.GetReadOnly<Health>(caster).Mana >= amount);

  // Deducts `amount` and returns true when the pool covers it; returns false and moves nothing
  // otherwise. A cost of zero or less spends nothing and always succeeds.
  public static bool TrySpend(ref Frame frame, EntityRef caster, FP64 amount) {
    if (amount <= FP64.Zero)
      return true;
    if (!frame.Has<Health>(caster))
      return false;

    ref var pools = ref frame.Get<Health>(caster);
    if (pools.Mana < amount)
      return false;

    pools.Mana -= amount;
    return true;
  }

  // Returns mana actually restored, clamped to the headroom under Stats.MaxMana.
  public static FP64 Restore(ref Frame frame, EntityRef target, FP64 amount) {
    if (amount <= FP64.Zero || !frame.Has<Health>(target))
      return FP64.Zero;

    var headroom = GetMaxMana(ref frame, target) - frame.GetReadOnly<Health>(target).Mana;
    if (headroom <= FP64.Zero)
      return FP64.Zero;

    var restored = amount < headroom ? amount : headroom;
    frame.Get<Health>(target).Mana += restored;
    return restored;
  }

  public static void RestoreToFull(ref Frame frame, EntityRef target) {
    if (frame.Has<Health>(target))
      frame.Get<Health>(target).Mana = GetMaxMana(ref frame, target);
  }

  // Grows the pool and hands the same amount over as current mana, so a bigger max never reads as a
  // spend debt - mirrors HealthApplication.GrantMaxHealth. A negative amount shrinks the pool and
  // pulls current mana down with it, stopping at zero.
  public static void GrantMaxMana(ref Frame frame, EntityRef target, FP64 amount) {
    if (amount == FP64.Zero || !frame.Has<StatsComponent>(target))
      return;

    frame.Get<StatsComponent>(target).Add(StatType.MaxMana, amount);

    if (!frame.Has<Health>(target))
      return;

    ref var pools = ref frame.Get<Health>(target);
    if (amount > FP64.Zero) {
      pools.Mana += amount;
      return;
    }

    var max = GetMaxMana(ref frame, target);
    if (pools.Mana > max)
      pools.Mana = max < FP64.Zero ? FP64.Zero : max;
  }
}
