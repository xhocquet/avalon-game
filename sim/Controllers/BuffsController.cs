using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

// Applies timed stat buffs and reverts them at expiry
public static class BuffsController {
  // Applies a ranked buff spec
  public static bool ApplySpec(ref Frame frame, EntityRef entity, int sourceId, in BuffSpec spec,
    int rank, int durationTicks) {
    var magnitude = spec.MagnitudeAtRank(rank);
    return spec.Mode == BuffMode.Flat
      ? Apply(ref frame, entity, sourceId, spec.Stat, magnitude, FP64.Zero, durationTicks)
      : Apply(ref frame, entity, sourceId, spec.Stat, FP64.Zero, magnitude, durationTicks);
  }

  // Refreshes matching source/stat buffs; returns false for invalid or full buff lists
  public static bool Apply(ref Frame frame, EntityRef entity, int sourceId, StatType stat, FP64 flat,
    FP64 percent, int durationTicks) {
    if (sourceId == 0 || durationTicks <= 0 || !frame.Has<Stats>(entity))
      return false;

    if (flat == FP64.Zero && percent == FP64.Zero) // Do not reserve slots for no-op buffs
      return false;

    if (!frame.Has<StatBuffs>(entity))
      frame.Add(entity, new StatBuffs());

    ref var buffs = ref frame.Get<StatBuffs>(entity);
    ref var stats = ref frame.Get<Stats>(entity);

    var slot = buffs.FindSlot(sourceId, stat);
    if (slot >= 0)
      Revert(ref stats, ref buffs, slot); // Replace the existing buff
    else if ((slot = buffs.FindFreeSlot()) < 0)
      return false;

    // Refresh from the unbuffed stat value
    var before = stats.Get(stat);
    stats.Add(stat, flat + before * percent);

    // Preserve the clamped delta for revert
    buffs.Set(slot, sourceId, stat, stats.Get(stat) - before, frame.Tick + durationTicks);
    return true;
  }

  // TimedEffectSystem calls this once per tick
  public static void ExpireDue(ref Frame frame, EntityRef entity) {
    ref var buffs = ref frame.Get<StatBuffs>(entity);
    ref var stats = ref frame.Get<Stats>(entity);

    for (var i = 0; i < StatBuffs.MaxEntries; i++)
      if (buffs.IsExpired(i, frame.Tick))
        Revert(ref stats, ref buffs, i);
  }

  // Removes adverse stat buffs and returns their count
  public static int ClearHarmful(ref Frame frame, EntityRef entity) {
    if (!frame.Has<StatBuffs>(entity) || !frame.Has<Stats>(entity))
      return 0;

    ref var buffs = ref frame.Get<StatBuffs>(entity);
    ref var stats = ref frame.Get<Stats>(entity);

    var cleared = 0;
    for (var i = 0; i < StatBuffs.MaxEntries; i++) {
      if (!buffs.IsActive(i) || !IsAdverse(buffs.GetStat(i), buffs.GetApplied(i)))
        continue;

      Revert(ref stats, ref buffs, i);
      cleared++;
    }

    return cleared;
  }

  public static void ClearNegative(ref Frame frame, EntityRef entity) {
    ClearHarmful(ref frame, entity);
    SnareController.Clear(ref frame, entity);
    DamageOverTimeController.Clear(ref frame, entity);
    SilenceController.Clear(ref frame, entity);
  }

  // Reverts all buffs; used on death
  public static void ClearAll(ref Frame frame, EntityRef entity) {
    if (!frame.Has<StatBuffs>(entity) || !frame.Has<Stats>(entity))
      return;

    ref var buffs = ref frame.Get<StatBuffs>(entity);
    ref var stats = ref frame.Get<Stats>(entity);

    for (var i = 0; i < StatBuffs.MaxEntries; i++)
      if (buffs.IsActive(i))
        Revert(ref stats, ref buffs, i);
  }

  private static void Revert(ref Stats stats, ref StatBuffs buffs, int slot) {
    stats.Add(buffs.GetStat(slot), -buffs.GetApplied(slot));
    buffs.Clear(slot);
  }

  private static bool IsAdverse(StatType stat, FP64 applied) {
    if (applied == FP64.Zero)
      return false;

    return HigherIsBetter(stat) ? applied < FP64.Zero : applied > FP64.Zero;
  }

  private static bool HigherIsBetter(StatType stat) {
    return stat switch {
      StatType.AttackWindup => false, // Seconds between a swing starting and landing
      StatType.GameplayRadius => false, // A bigger hurtbox is worse
      _ => true
    };
  }
}
