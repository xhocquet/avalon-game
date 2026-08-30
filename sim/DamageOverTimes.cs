using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

// The one place a lingering burn goes on a unit or ticks down, the delayed-damage sibling of
// DamageApplication. The DamageOverTime write and every payout run through here, so a burn
// cannot leave a rate on a unit with nothing paying it out.
//
// Duration is an absolute expiry tick, not a countdown, so a rollback replay ends the burn on the tick
// it first did. Damage accrues every tick but is only dealt on the PayoutIntervalMs boundary, plus a
// final instalment on the expiry tick for whatever accrued since the last one - the target takes a few
// solid hits over the window rather than a floored point every frame. The accrual stays per-tick so
// the total is rate x duration regardless of how the interval divides the window.
public static class DamageOverTimes {
  // How often an accrued burn is handed to DamageApplication. The DoT block is authored per second, so
  // the payout cadence matches. Lower it for snappier feedback; the total damage is unaffected.
  public const int PayoutIntervalMs = 1000;

  // Starts (or refreshes) a burn dealing damagePerSecond magical damage for durationTicks. Returns
  // false when the burn is a no-op.
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

  // One tick's accrual, plus a payout when one is due. TimedEffectSystem calls this each tick for every
  // burning unit, ahead of DeathSystem, so a lethal instalment still resolves the kill on the frame.
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

    // Every DamageOverTime write is done above: ApplyDamage can allocate the hit-id singleton
    // on its first call of the match, which invalidates the `dot` ref into component storage.
    if (payout <= FP64.Zero)
      return;

    var source = UnitLookup.TryGetEntityByUnitId(ref frame, sourceUnitId, out var s) ? s : default;
    DamageApplication.ApplyDamage(ref frame, source, entity, payout, DamageType.Magical);
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
