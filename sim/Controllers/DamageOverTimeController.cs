using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

public static class DamageOverTimeController {
  public const int PayoutIntervalMs = 1000;

  public static bool Apply(ref Frame frame, EntityRef target, EntityRef source, int sourceSkillAssetId,
    FP64 damagePerSecond, int durationTicks) {
    var targetUnitId = UnitLookup.GetUnitId(ref frame, target);
    if (sourceSkillAssetId == 0 || targetUnitId == 0 || durationTicks <= 0 || damagePerSecond <= FP64.Zero ||
        !frame.Has<Health>(target) || !frame.GetReadOnly<Health>(target).IsAlive)
      return false;

    var effect = Find(ref frame, targetUnitId, sourceSkillAssetId);
    if (!effect.IsValid) {
      var intervalTicks = TickMath.MsToTicksCeil(ref frame, PayoutIntervalMs);
      if (intervalTicks < 1)
        intervalTicks = 1;
      effect = frame.CreateEntity();
      frame.Add(effect, new DamageOverTime {
        TargetUnitId = targetUnitId,
        SourceSkillAssetId = sourceSkillAssetId,
        IntervalTicks = intervalTicks,
        NextPayoutTick = frame.Tick + intervalTicks
      });
    }

    ref var dot = ref frame.Get<DamageOverTime>(effect);
    dot.SourceUnitId = UnitLookup.GetUnitId(ref frame, source);
    dot.StartedTick = frame.Tick;
    dot.ExpiryTick = frame.Tick + durationTicks;
    dot.AccrualPerTick = damagePerSecond * FP64.FromInt(TickMath.DeltaTimeMs(ref frame)) / FP64.FromInt(1000);
    return true;
  }

  public static void Tick(ref Frame frame, EntityRef effect, UnitLookup.Index units) {
    var dot = frame.GetReadOnly<DamageOverTime>(effect);
    if (!dot.IsBurning || !units.TryGet(dot.TargetUnitId, out var target) ||
        !frame.Has<Health>(target) || !frame.GetReadOnly<Health>(target).IsAlive) {
      frame.DestroyEntity(effect);
      return;
    }

    var expired = dot.IsExpired(frame.Tick);
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

    if (expired)
      frame.DestroyEntity(effect);
    else
      frame.Get<DamageOverTime>(effect) = dot;

    if (payout <= FP64.Zero)
      return;

    var source = units.TryGet(dot.SourceUnitId, out var s) ? s : default;
    DamageController.ApplyDamage(ref frame, source, target, payout, DamageType.Magical);
  }

  public static void Clear(ref Frame frame, EntityRef target) {
    var targetUnitId = UnitLookup.GetUnitId(ref frame, target);
    if (targetUnitId == 0)
      return;
    var filter = frame.Filter<DamageOverTime>();
    while (filter.Next(out var effect)) {
      ref var dot = ref frame.Get<DamageOverTime>(effect);
      if (dot.TargetUnitId == targetUnitId)
        dot.Clear(); // Reaped by TimedEffectSystem on its next pass
    }
  }

  public static bool IsBurning(ref Frame frame, EntityRef target) {
    var targetUnitId = UnitLookup.GetUnitId(ref frame, target);
    if (targetUnitId == 0)
      return false;
    var filter = frame.Filter<DamageOverTime>();
    while (filter.Next(out var effect)) {
      ref readonly var dot = ref frame.GetReadOnly<DamageOverTime>(effect);
      if (dot.TargetUnitId == targetUnitId && dot.IsBurning)
        return true;
    }
    return false;
  }

  private static EntityRef Find(ref Frame frame, int targetUnitId, int sourceSkillAssetId) {
    var filter = frame.Filter<DamageOverTime>();
    while (filter.Next(out var effect)) {
      ref readonly var dot = ref frame.GetReadOnly<DamageOverTime>(effect);
      if (dot.TargetUnitId == targetUnitId && dot.SourceSkillAssetId == sourceSkillAssetId)
        return effect;
    }
    return default;
  }
}
