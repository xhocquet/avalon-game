using System.Collections.Generic;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Tests;

internal static class BuffTestQueries {
  internal readonly record struct Entry(int SourceId, StatType Stat, FP64 Magnitude, int ExpiryTick);

  internal static List<Entry> StatChanges(Frame frame, EntityRef entity) {
    var entries = new List<Entry>();
    if (!frame.Has<Buffs>(entity) || !frame.Has<BuffChanges>(entity))
      return entries;

    ref readonly var buffs = ref frame.GetReadOnly<Buffs>(entity);
    ref readonly var changes = ref frame.GetReadOnly<BuffChanges>(entity);
    for (var i = 0; i < BuffChanges.MaxEntries; i++) {
      if (!changes.IsActive(i))
        continue;
      var change = changes.GetChange(i);
      if (!change.IsStat)
        continue;
      var buffIndex = changes.GetBuffIndex(i);
      entries.Add(new Entry(buffs.GetSourceSkillAssetId(buffIndex), (StatType)change.Parameter, change.Magnitude,
        buffs.GetExpiryTick(buffIndex)));
    }
    return entries;
  }
}
