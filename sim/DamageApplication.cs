using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

// The one place health goes down. Auto-attacks (DamageSystem) and skills both route through here so
// mitigation, the zero floor, kill credit, and the hit event can never drift apart between them.
public static class DamageApplication {
  private static readonly FP64 Hundred = FP64.FromInt(100);
  private static readonly FP64 Two = FP64.FromInt(2);

  // Returns the damage actually dealt after mitigation.
  public static FP64 ApplyDamage(ref Frame frame, EntityRef source, EntityRef target, FP64 amount,
    DamageType damageType = DamageType.Physical) {
    var sourceUnitId = UnitLookup.GetUnitId(ref frame, source);

    // Godmode still raises the hit so attack VFX and feedback play; only the health write is skipped,
    // which also leaves LastDamagerUnitId alone and keeps kill credit off an attacker who dealt nothing.
    if (Cheats.BlocksDamage(ref frame, target)) {
      RaiseHitEvent(ref frame, source, target, sourceUnitId, FP64.Zero);
      return FP64.Zero;
    }

    var damage = Mitigate(ref frame, target, amount, damageType);

    ref var health = ref frame.Get<Health>(target);
    health.Current -= damage;
    if (health.Current < FP64.Zero)
      health.Current = FP64.Zero;

    // Load-bearing, not bookkeeping: DeathSystem and RespawnSystem read this to resolve the killer,
    // and ExperienceRewards.AwardForKill pays out against it. A kill that skips it awards nobody.
    health.LastDamagerUnitId = sourceUnitId;

    MatchStats.RecordDamage(ref frame, source, target, damage);
    RaiseHitEvent(ref frame, source, target, sourceUnitId, damage);
    return damage;
  }

  // Resists scale by a fraction rather than subtracting flat, so stacking approaches but never
  // reaches immunity and low-damage attackers stay relevant. Negative resist is the same curve
  // mirrored - it amplifies toward 2x rather than jumping there, and never inverts the sign.
  public static FP64 Mitigate(ref Frame frame, EntityRef target, FP64 damage,
    DamageType damageType = DamageType.Physical) {
    if (damage <= FP64.Zero || !frame.Has<StatsComponent>(target))
      return damage;

    var stats = frame.GetReadOnly<StatsComponent>(target);
    var resist = damageType == DamageType.Magical ? stats.MagicResist : stats.Armor;

    var multiplier = resist >= FP64.Zero
      ? Hundred / (Hundred + resist)
      : Two - Hundred / (Hundred - resist);

    var mitigated = damage * multiplier;
    return mitigated < FP64.One ? FP64.One : mitigated; // Floor at 1 damage
  }

  private static void RaiseHitEvent(ref Frame frame, EntityRef source, EntityRef target,
    int sourceUnitId, FP64 damage) {
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
