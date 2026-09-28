using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

// Mana changes go through this controller
public static class ManaController {
  private static FP64 GetMaxMana(ref Frame frame, EntityRef target) =>
    frame.Has<Stats>(target) ? frame.GetReadOnly<Stats>(target).MaxMana : FP64.Zero;

  // Units without Stats have no mana pool
  // Read-only affordability check; non-positive costs are free
  public static bool CanAfford(ref Frame frame, EntityRef caster, FP64 amount) =>
    amount <= FP64.Zero ||
    (frame.Has<Health>(caster) && frame.GetReadOnly<Health>(caster).Mana >= amount);

  // Spends mana when available; non-positive costs are free
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

  // Returns restored mana
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

  // Max-mana changes shift current mana by the same amount; reductions clamp at zero
  public static void GrantMaxMana(ref Frame frame, EntityRef target, FP64 amount) {
    if (amount == FP64.Zero || !frame.Has<Stats>(target))
      return;

    frame.Get<Stats>(target).Add(StatType.MaxMana, amount);

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
