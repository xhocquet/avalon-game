using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

// Applies buff changes and reverts them at expiry.
public static class BuffsController {
  public static void ApplySkill(ref Frame frame, EntityRef target, SkillAsset skill, int rank) {
    var durationTicks = TickMath.MsToTicksCeil(ref frame, skill.BuffDurationMsAtRank(rank));
    foreach (var spec in skill.BuffSpecs)
      ApplySpec(ref frame, target, skill.AssetId, spec, rank, durationTicks);
  }

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

    if (!frame.Has<Buffs>(entity))
      frame.Add(entity, new Buffs());
    if (!frame.Has<BuffChanges>(entity))
      frame.Add(entity, new BuffChanges());

    ref var buffs = ref frame.Get<Buffs>(entity);
    ref var changes = ref frame.Get<BuffChanges>(entity);
    ref var stats = ref frame.Get<Stats>(entity);

    var slot = FindStatChange(in buffs, in changes, sourceId, stat);
    if (slot >= 0)
      Revert(ref stats, ref buffs, ref changes, slot);
    else if ((slot = changes.FindFreeSlot()) < 0)
      return false;

    var expiryTick = frame.Tick + durationTicks;
    var buffIndex = buffs.FindSlot(sourceId, expiryTick);
    if (buffIndex < 0) {
      buffIndex = buffs.FindFreeSlot();
      if (buffIndex < 0)
        return false;
      buffs.Set(buffIndex, sourceId, frame.Tick, expiryTick);
    }

    // Refresh from the unbuffed stat value
    var before = stats.Get(stat);
    stats.Add(stat, flat + before * percent);

    // Preserve the clamped delta for revert
    changes.Set(slot, buffIndex, BuffChange.Stat(stat, stats.Get(stat) - before));
    return true;
  }

  // TimedEffectSystem calls this once per tick
  public static void ExpireDue(ref Frame frame, EntityRef entity) {
    ref var buffs = ref frame.Get<Buffs>(entity);
    ref var changes = ref frame.Get<BuffChanges>(entity);
    ref var stats = ref frame.Get<Stats>(entity);

    for (var i = 0; i < BuffChanges.MaxEntries; i++)
      if (changes.IsActive(i) && buffs.IsExpired(changes.GetBuffIndex(i), frame.Tick))
        Revert(ref stats, ref buffs, ref changes, i);
  }

  // Removes adverse stat buffs and returns their count
  public static int ClearHarmful(ref Frame frame, EntityRef entity) {
    if (!frame.Has<Buffs>(entity) || !frame.Has<BuffChanges>(entity) || !frame.Has<Stats>(entity))
      return 0;

    ref var buffs = ref frame.Get<Buffs>(entity);
    ref var changes = ref frame.Get<BuffChanges>(entity);
    ref var stats = ref frame.Get<Stats>(entity);

    var cleared = 0;
    for (var i = 0; i < BuffChanges.MaxEntries; i++) {
      if (!changes.IsActive(i) || changes.GetChange(i).Polarity != EffectPolarity.Debuff)
        continue;

      Revert(ref stats, ref buffs, ref changes, i);
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
    if (!frame.Has<Buffs>(entity) || !frame.Has<BuffChanges>(entity) || !frame.Has<Stats>(entity))
      return;

    ref var buffs = ref frame.Get<Buffs>(entity);
    ref var changes = ref frame.Get<BuffChanges>(entity);
    ref var stats = ref frame.Get<Stats>(entity);

    for (var i = 0; i < BuffChanges.MaxEntries; i++)
      if (changes.IsActive(i))
        Revert(ref stats, ref buffs, ref changes, i);
  }

  private static int FindStatChange(in Buffs buffs, in BuffChanges changes, int sourceId, StatType stat) {
    for (var i = 0; i < Buffs.MaxEntries; i++) {
      if (buffs.GetSourceSkillAssetId(i) != sourceId)
        continue;
      var slot = changes.FindSlot(i, BuffChangeKind.Stat, (int)stat);
      if (slot >= 0)
        return slot;
    }
    return -1;
  }

  private static void Revert(ref Stats stats, ref Buffs buffs, ref BuffChanges changes, int slot) {
    var change = changes.GetChange(slot);
    if (change.Kind == BuffChangeKind.Stat)
      stats.Add((StatType)change.Parameter, -change.Applied);

    var buffIndex = changes.GetBuffIndex(slot);
    changes.Clear(slot);
    if (!changes.HasChanges(buffIndex))
      buffs.Clear(buffIndex);
  }
}
