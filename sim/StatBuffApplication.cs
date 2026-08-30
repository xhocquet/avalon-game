using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

// The one place a timed stat modifier goes on or comes off, the buff counterpart to DamageApplication:
// the StatsComponent write and the StatBuffsComponent bookkeeping always happen together, so nothing
// can leave a stat raised with no entry to take it back down.
//
// Duration is stored as the absolute tick the buff ends on, not a countdown, so TimedEffectSystem is a
// comparison per entry per tick and a rollback replay lands on the same expiry tick it did the first
// time. A buff applied on tick N with a duration of D ticks is worth its bonus on ticks N..N+D-1.
public static class StatBuffApplication {
  // Adds a fraction of the stat's current value. The fraction is resolved once, at apply time, and the
  // resulting amount is then held fixed - the hero levelling mid-buff does not retroactively grow it.
  public static bool ApplyPercent(ref Frame frame, EntityRef entity, int sourceId, StatType stat,
    FP64 percent, int durationTicks) {
    return Apply(ref frame, entity, sourceId, stat, FP64.Zero, percent, durationTicks);
  }

  public static bool ApplyFlat(ref Frame frame, EntityRef entity, int sourceId, StatType stat,
    FP64 delta, int durationTicks) {
    return Apply(ref frame, entity, sourceId, stat, delta, FP64.Zero, durationTicks);
  }

  // Applies one parsed BuffStats entry at the given rank, routing flat/percent to the right overload.
  public static bool ApplySpec(ref Frame frame, EntityRef entity, int sourceId, in BuffSpec spec,
    int rank, int durationTicks) {
    var magnitude = spec.MagnitudeAtRank(rank);
    return spec.Mode == BuffMode.Flat
      ? Apply(ref frame, entity, sourceId, spec.Stat, magnitude, FP64.Zero, durationTicks)
      : Apply(ref frame, entity, sourceId, spec.Stat, FP64.Zero, magnitude, durationTicks);
  }

  // Returns false when the unit carries no stats, the buff is a no-op, or the entity is already
  // holding MaxEntries buffs - the last one is a capacity limit rather than a rule, so a caller that
  // cares should log it.
  public static bool Apply(ref Frame frame, EntityRef entity, int sourceId, StatType stat, FP64 flat,
    FP64 percent, int durationTicks) {
    if (sourceId == 0 || durationTicks <= 0 || !frame.Has<StatsComponent>(entity))
      return false;

    if (flat == FP64.Zero && percent == FP64.Zero) // A zero buff would hold a slot for nothing
      return false;

    if (!frame.Has<StatBuffsComponent>(entity))
      frame.Add(entity, new StatBuffsComponent());

    ref var buffs = ref frame.Get<StatBuffsComponent>(entity);
    ref var stats = ref frame.Get<StatsComponent>(entity);

    var slot = buffs.FindSlot(sourceId, stat);
    if (slot >= 0)
      Revert(ref stats, ref buffs, slot); // Refresh: the running copy comes off before the new one lands
    else if ((slot = buffs.FindFreeSlot()) < 0)
      return false;

    // Read after that revert, so a recast is worth a percentage of the unbuffed stat rather than
    // compounding on the copy it is replacing.
    var before = stats.Get(stat);
    stats.Add(stat, flat + before * percent);

    // StatRanges clamps inside Set, which can swallow part of the delta. Record what the stat actually
    // moved by rather than what was asked for, or the revert hands back value that was never granted.
    buffs.Set(slot, sourceId, stat, stats.Get(stat) - before, frame.Tick + durationTicks);
    return true;
  }

  // Takes off every entry whose expiry tick has arrived. Called once per tick by TimedEffectSystem.
  public static void ExpireDue(ref Frame frame, EntityRef entity) {
    ref var buffs = ref frame.Get<StatBuffsComponent>(entity);
    ref var stats = ref frame.Get<StatsComponent>(entity);

    for (var i = 0; i < StatBuffsComponent.MaxEntries; i++)
      if (buffs.IsExpired(i, frame.Tick))
        Revert(ref stats, ref buffs, i);
  }

  // Drops every buff early, reverting each one. Death goes through here rather than letting the timers
  // run out on a corpse.
  public static void ClearAll(ref Frame frame, EntityRef entity) {
    if (!frame.Has<StatBuffsComponent>(entity) || !frame.Has<StatsComponent>(entity))
      return;

    ref var buffs = ref frame.Get<StatBuffsComponent>(entity);
    ref var stats = ref frame.Get<StatsComponent>(entity);

    for (var i = 0; i < StatBuffsComponent.MaxEntries; i++)
      if (buffs.IsActive(i))
        Revert(ref stats, ref buffs, i);
  }

  public static int ActiveCount(ref Frame frame, EntityRef entity) {
    if (!frame.Has<StatBuffsComponent>(entity))
      return 0;

    ref readonly var buffs = ref frame.GetReadOnly<StatBuffsComponent>(entity);
    var count = 0;
    for (var i = 0; i < StatBuffsComponent.MaxEntries; i++)
      if (buffs.IsActive(i))
        count++;

    return count;
  }

  private static void Revert(ref StatsComponent stats, ref StatBuffsComponent buffs, int slot) {
    stats.Add(buffs.GetStat(slot), -buffs.GetApplied(slot));
    buffs.Clear(slot);
  }
}
