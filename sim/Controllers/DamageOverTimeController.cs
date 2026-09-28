using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

public static class DamageOverTimeController {
  public const int PayoutIntervalMs = 1000;

  public static bool Apply(ref Frame frame, EntityRef target, EntityRef source, int sourceId,
    FP64 damagePerSecond, int durationTicks) {
    if (sourceId == 0 || durationTicks <= 0 || damagePerSecond <= FP64.Zero || !frame.Has<Health>(target))
      return false;

    if (!frame.Has<DamageOverTime>(target))
      frame.Add(target, new DamageOverTime());

    var intervalTicks = TickMath.MsToTicksCeil(ref frame, PayoutIntervalMs);
    if (intervalTicks < 1)
      intervalTicks = 1;

    ref var dot = ref frame.Get<DamageOverTime>(target);
    dot.SourceId = sourceId;
    dot.SourceUnitId = UnitLookup.GetUnitId(ref frame, source);
    dot.ExpiryTick = frame.Tick + durationTicks;
    dot.IntervalTicks = intervalTicks;
    dot.NextPayoutTick = frame.Tick + intervalTicks;
    dot.AccrualPerTick = damagePerSecond * FP64.FromInt(TickMath.DeltaTimeMs(ref frame)) / FP64.FromInt(1000);
    dot.Pending = FP64.Zero;
    return true;
  }

  public static void Tick(ref Frame frame, EntityRef entity) {
    ref var dot = ref frame.Get<DamageOverTime>(entity);
    if (!dot.IsBurning)
      return;

    var expired = frame.Tick >= dot.ExpiryTick;
    if (!expired)
      dot.Pending += dot.AccrualPerTick;

    var payout = FP64.Zero;
    if (expired || frame.Tick >= dot.NextPayoutTick) {
      var whole = FP64.Floor(dot.Pending);
      if (whole >= FP64.One) {
        dot.Pending -= whole;
        payout = whole;
      }

      dot.NextPayoutTick += dot.IntervalTicks;
    }

    var sourceUnitId = dot.SourceUnitId;
    if (expired)
      dot.Clear();

    // ApplyDamage can invalidate the component ref
    if (payout <= FP64.Zero)
      return;

    var source = UnitLookup.TryGetEntityByUnitId(ref frame, sourceUnitId, out var s) ? s : default;
    DamageController.ApplyDamage(ref frame, source, entity, payout, DamageType.Magical);
  }

  public static void Clear(ref Frame frame, EntityRef entity) {
    if (frame.Has<DamageOverTime>(entity))
      frame.Get<DamageOverTime>(entity).Clear();
  }

  public static bool IsBurning(ref Frame frame, EntityRef entity) {
    return frame.Has<DamageOverTime>(entity) &&
           frame.GetReadOnly<DamageOverTime>(entity).IsBurning;
  }
}
