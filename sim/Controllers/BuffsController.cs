using System;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

// Applies buff changes and reverts them at expiry.
public static class BuffsController {
  public static bool ApplySkill(ref Frame frame, EntityRef target, SkillAsset skill, int rank) {
    var durationTicks = TickMath.MsToTicksCeil(ref frame, skill.BuffDurationMsAtRank(rank));
    return Apply(ref frame, target, skill.AssetId, skill.BuffSpecs, rank, durationTicks);
  }

  // Applies one complete buff, replacing the same skill's existing buff.
  public static bool Apply(ref Frame frame, EntityRef entity, int sourceSkillAssetId,
    ReadOnlySpan<BuffSpec> specs, int rank, int durationTicks) {
    if (sourceSkillAssetId == 0 || rank <= 0 || durationTicks <= 0 || !frame.Has<Stats>(entity))
      return false;

    var required = 0;
    foreach (var spec in specs) {
      if ((uint)spec.Stat >= (uint)StatType.StatCount || spec.Mode is not (BuffMode.Flat or BuffMode.Percent))
        return false;
      if (spec.MagnitudeAtRank(rank) != FP64.Zero)
        required++;
    }
    if (required == 0 || required > BuffChanges.MaxEntries)
      return false;

    var buffs = frame.Has<Buffs>(entity) ? frame.GetReadOnly<Buffs>(entity) : default;
    var changes = frame.Has<BuffChanges>(entity) ? frame.GetReadOnly<BuffChanges>(entity) : default;
    var stats = frame.GetReadOnly<Stats>(entity);
    var buffIndex = buffs.FindSlot(sourceSkillAssetId);
    if (buffIndex >= 0) {
      for (var i = 0; i < BuffChanges.MaxEntries; i++)
        if (changes.IsActive(i) && changes.GetBuffIndex(i) == buffIndex)
          Revert(ref stats, ref buffs, ref changes, i);
    }
    else {
      buffIndex = buffs.FindFreeSlot();
      if (buffIndex < 0)
        return false;
    }

    buffs.Set(buffIndex, sourceSkillAssetId, frame.Tick, frame.Tick + durationTicks);
    foreach (var spec in specs) {
      var magnitude = spec.MagnitudeAtRank(rank);
      if (magnitude == FP64.Zero)
        continue;
      var slot = changes.FindFreeSlot();
      if (slot < 0)
        return false;

      var before = stats.Get(spec.Stat);
      stats.Add(spec.Stat, spec.Mode == BuffMode.Flat ? magnitude : before * magnitude);
      changes.Set(slot, buffIndex, BuffChange.Stat(spec.Stat, stats.Get(spec.Stat) - before));
    }

    // Commit the staged components only after every change fits.
    if (!frame.Has<Buffs>(entity))
      frame.Add(entity, buffs);
    else
      frame.Get<Buffs>(entity) = buffs;
    if (!frame.Has<BuffChanges>(entity))
      frame.Add(entity, changes);
    else
      frame.Get<BuffChanges>(entity) = changes;
    frame.Get<Stats>(entity) = stats;
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
