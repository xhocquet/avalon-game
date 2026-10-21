using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

public static class StatsController {
  public static FP64 GetBase(ref Frame frame, EntityRef entity, StatType stat) =>
    frame.Has<BaseStats>(entity)
      ? frame.GetReadOnly<BaseStats>(entity).Get(stat)
      : frame.GetReadOnly<Stats>(entity).Get(stat);

  public static void AddBase(ref Frame frame, EntityRef entity, StatType stat, FP64 amount) =>
    SetBase(ref frame, entity, stat, GetBase(ref frame, entity, stat) + amount);

  public static void SetBase(ref Frame frame, EntityRef entity, StatType stat, FP64 value) {
    if (!frame.Has<BaseStats>(entity)) {
      frame.Get<Stats>(entity).Set(stat, value);
      return;
    }

    ref var baseline = ref frame.Get<BaseStats>(entity);
    baseline.Set(stat, value);
    ref readonly var changes = ref frame.GetReadOnly<BuffChanges>(entity);
    frame.Get<Stats>(entity).Set(stat, CalculateStat(in baseline, in changes, stat));
  }

  internal static Stats Calculate(in BaseStats baseline, in BuffChanges changes) {
    var stats = Stats.Create();
    for (var i = 0; i < StatRanges.Count; i++) {
      var stat = (StatType)i;
      stats.Set(stat, CalculateStat(in baseline, in changes, stat));
    }
    return stats;
  }

  internal static void Recalculate(ref Frame frame, EntityRef entity) {
    if (!frame.Has<BaseStats>(entity))
      return;
    var baseline = frame.GetReadOnly<BaseStats>(entity);
    var changes = frame.GetReadOnly<BuffChanges>(entity);
    frame.Get<Stats>(entity) = Calculate(in baseline, in changes);
    if (!changes.HasStatChanges)
      frame.Remove<BaseStats>(entity);
  }

  private static FP64 CalculateStat(in BaseStats baseline, in BuffChanges changes, StatType stat) {
    var flat = FP64.Zero;
    var percent = FP64.Zero;
    for (var i = 0; i < BuffChanges.MaxEntries; i++) {
      if (!changes.IsActive(i))
        continue;
      var change = changes.GetChange(i);
      if (change.Parameter != (int)stat)
        continue;
      if (change.Kind == BuffChangeKind.StatFlat)
        flat += change.Magnitude;
      else if (change.Kind == BuffChangeKind.StatPercent)
        percent += change.Magnitude;
    }
    var value = baseline.Get(stat);
    return value + value * percent + flat;
  }
}
