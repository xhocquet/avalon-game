using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

// Health increases go through this controller; damage uses DamageController
public static class HealthController {
  // Returns restored HP; does not revive dead units
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

  // Respawn path; restores health from zero
  public static void RestoreToFull(ref Frame frame, EntityRef target) {
    if (frame.Has<Health>(target))
      frame.Get<Health>(target).Current = GetMaxHealth(ref frame, target);
  }

  // Max-health changes shift current HP by the same amount; reductions clamp at 1
  public static void GrantMaxHealth(ref Frame frame, EntityRef target, FP64 amount) {
    if (amount == FP64.Zero || !frame.Has<Stats>(target))
      return;

    frame.Get<Stats>(target).Add(StatType.MaxHealth, amount);

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

  // Units without Stats have no health pool
  public static FP64 GetMaxHealth(ref Frame frame, EntityRef target) =>
    frame.Has<Stats>(target) ? frame.GetReadOnly<Stats>(target).MaxHealth : FP64.Zero;
}
