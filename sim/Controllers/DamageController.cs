using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

public static class DamageController {
  private static readonly FP64 Hundred = FP64.FromInt(100);
  private static readonly FP64 Two = FP64.FromInt(2);

  // attackHitId links pre-hit effect events
  public static FP64 ApplyDamage(ref Frame frame, EntityRef source, EntityRef target, FP64 amount,
    DamageType damageType = DamageType.Physical, bool canCrit = false, int attackHitId = 0) {
    var sourceUnitId = UnitLookup.GetUnitId(ref frame, source);
    if (attackHitId == 0)
      attackHitId = NextHitId(ref frame);

    if (CheatsController.BlocksDamage(ref frame, target)) {
      RaiseHitEvent(ref frame, source, target, sourceUnitId, FP64.Zero, false, attackHitId);
      return FP64.Zero;
    }

    var isCrit = false;
    var incoming = canCrit
      ? CriticalStrikes.Scale(ref frame, source, sourceUnitId, amount, out isCrit)
      : amount;

    var damage = Mitigate(ref frame, target, incoming, damageType);

    ref var health = ref frame.Get<Health>(target);
    health.Current -= damage;
    if (health.Current < FP64.Zero)
      health.Current = FP64.Zero;

    health.LastDamagerUnitId = sourceUnitId;

    MatchStatsController.RecordDamage(ref frame, source, target, damage);
    RaiseHitEvent(ref frame, source, target, sourceUnitId, damage, isCrit, attackHitId);
    return damage;
  }

  public static FP64 Mitigate(ref Frame frame, EntityRef target, FP64 damage,
    DamageType damageType = DamageType.Physical) {
    if (damage <= FP64.Zero || !frame.Has<Stats>(target))
      return damage;

    var stats = frame.GetReadOnly<Stats>(target);
    var resist = damageType == DamageType.Magical ? stats.MagicResist : stats.Armor;

    var multiplier = resist >= FP64.Zero
      ? Hundred / (Hundred + resist)
      : Two - Hundred / (Hundred - resist);

    var mitigated = damage * multiplier;
    return mitigated < FP64.One ? FP64.One : mitigated; // Floor at 1 damage
  }

  // Always allocate; the counter is frame state
  public static int NextHitId(ref Frame frame) {
    return IdCounter<AttackHitIdCounter>.Next(ref frame);
  }

  private static void RaiseHitEvent(ref Frame frame, EntityRef source, EntityRef target,
    int sourceUnitId, FP64 damage, bool isCrit, int attackHitId) {
    if (frame.EventRaiser == null)
      return;

    var evt = EventPool.Get<AttackHitEvent>();
    evt.AttackerUnitId = sourceUnitId;
    evt.TargetUnitId = UnitLookup.GetUnitId(ref frame, target);
    evt.Damage = damage;
    evt.IsCrit = isCrit ? 1 : 0;
    evt.AttackHitId = attackHitId;
    evt.AttackerPosition = frame.Has<TransformComponent>(source)
      ? frame.GetReadOnly<TransformComponent>(source).Position
      : default;
    evt.TargetPosition = frame.Has<TransformComponent>(target)
      ? frame.GetReadOnly<TransformComponent>(target).Position
      : default;
    frame.EventRaiser.RaiseEvent(evt);
  }
}
