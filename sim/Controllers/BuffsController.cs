using System;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using Meesles.Avalon.Sim.Heroes;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

// Applies buff changes and reverts them at expiry.
public static class BuffsController {
  public static bool ApplySkill(ref Frame frame, in SkillCastContext ctx) =>
    ApplySkill(ref frame, ctx.Caster, in ctx);

  public static bool ApplySkill(ref Frame frame, EntityRef target, in SkillCastContext ctx) =>
    ApplySkill(ref frame, target, ctx.Skill, ctx.Rank, UnitLookup.GetUnitId(ref frame, ctx.Caster));

  public static bool ApplySkill(ref Frame frame, EntityRef target, SkillAsset skill, int rank,
    int sourceUnitId = 0) {
    var durationTicks = TickMath.MsToTicksCeil(ref frame, skill.BuffDurationMsAtRank(rank));
    return Apply(ref frame, target, skill.AssetId, skill.BuffSpecs, rank, durationTicks,
      sourceUnitId, skill.BuffPolicy);
  }

  // Stages one complete buff application before committing any frame state.
  public static bool Apply(ref Frame frame, EntityRef entity, int sourceSkillAssetId,
    ReadOnlySpan<BuffSpec> specs, int rank, int durationTicks, int sourceUnitId = 0,
    BuffApplicationPolicy? applicationPolicy = null) {
    var policy = applicationPolicy ?? BuffApplicationPolicy.Default;
    if (sourceSkillAssetId == 0 || rank <= 0 || durationTicks <= 0 ||
        durationTicks > int.MaxValue - frame.Tick || !frame.Has<Stats>(entity) || !policy.IsValid ||
        sourceUnitId < 0 || (policy.Identity == BuffIdentity.SkillPerCaster && sourceUnitId == 0))
      return false;

    if (!HasValidChanges(specs, rank))
      return false;

    var buffs = frame.Has<Buffs>(entity) ? frame.GetReadOnly<Buffs>(entity) : default;
    var changes = frame.Has<BuffChanges>(entity) ? frame.GetReadOnly<BuffChanges>(entity) : default;
    var stats = frame.GetReadOnly<Stats>(entity);
    RevertExpired(ref stats, ref buffs, ref changes, frame.Tick);

    if (!TrySelectBuff(in buffs, sourceSkillAssetId, sourceUnitId, in policy, out var selection))
      return false;
    if (!TryGetLifetime(in buffs, selection, policy.Duration, frame.Tick, durationTicks, out var lifetime))
      return false;

    if (buffs.IsActive(selection.Index))
      RevertBuff(ref stats, ref buffs, ref changes, selection.Index);
    buffs.Set(selection.Index, sourceSkillAssetId, lifetime.Start, lifetime.Expiry, sourceUnitId, rank);

    if (!TryApplyChanges(ref stats, ref changes, selection.Index, specs, rank))
      return false;

    Commit(ref frame, entity, in buffs, in changes, in stats);
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

  private static bool HasValidChanges(ReadOnlySpan<BuffSpec> specs, int rank) {
    var required = 0;
    foreach (var spec in specs) {
      if ((uint)spec.Stat >= (uint)StatType.StatCount || spec.Mode is not (BuffMode.Flat or BuffMode.Percent))
        return false;
      if (spec.MagnitudeAtRank(rank) != FP64.Zero)
        required++;
    }

    return required > 0 && required <= BuffChanges.MaxEntries;
  }

  private static (int Count, int Oldest, int Weakest) FindMatchingBuffs(in Buffs buffs,
    int sourceSkillAssetId, int sourceUnitId, BuffIdentity identity) {
    var count = 0;
    var oldest = -1;
    var weakest = -1;
    for (var i = 0; i < Buffs.MaxEntries; i++) {
      if (!buffs.IsActive(i) || buffs.GetSourceSkillAssetId(i) != sourceSkillAssetId ||
          (identity == BuffIdentity.SkillPerCaster && buffs.GetSourceUnitId(i) != sourceUnitId))
        continue;
      count++;
      if (oldest < 0 || buffs.GetStartedTick(i) < buffs.GetStartedTick(oldest))
        oldest = i;
      if (weakest < 0 || buffs.GetRank(i) < buffs.GetRank(weakest) ||
          (buffs.GetRank(i) == buffs.GetRank(weakest) && buffs.GetStartedTick(i) < buffs.GetStartedTick(weakest)))
        weakest = i;
    }

    return (count, oldest, weakest);
  }

  private readonly record struct BuffSelection(int Index, bool RestartLifetime);

  private static bool TrySelectBuff(in Buffs buffs, int sourceSkillAssetId, int sourceUnitId,
    in BuffApplicationPolicy policy, out BuffSelection selection) {
    var matches = FindMatchingBuffs(in buffs, sourceSkillAssetId, sourceUnitId, policy.Identity);
    selection = default;
    if (matches.Count == 0 ||
        (policy.Reapplication == BuffReapplication.Stack && matches.Count < policy.MaxStacks)) {
      var index = buffs.FindFreeSlot();
      if (index < 0)
        return false;
      selection = new BuffSelection(index, true);
      return true;
    }

    if (policy.Reapplication == BuffReapplication.Ignore)
      return false;
    if (policy.Reapplication == BuffReapplication.Refresh || policy.Overflow == BuffStackOverflow.RefreshOldest) {
      selection = new BuffSelection(matches.Oldest, false);
      return true;
    }

    if (policy.Overflow == BuffStackOverflow.ReplaceWeakest) {
      selection = new BuffSelection(matches.Weakest, true);
      return true;
    }

    return false;
  }

  private static bool TryGetLifetime(in Buffs buffs, BuffSelection selection, BuffDurationPolicy policy,
    int tick, int durationTicks, out (int Start, int Expiry) lifetime) {
    lifetime = (tick, tick + durationTicks);
    if (selection.RestartLifetime)
      return true;

    var oldStart = buffs.GetStartedTick(selection.Index);
    var oldExpiry = buffs.GetExpiryTick(selection.Index);
    if (policy == BuffDurationPolicy.KeepLaterExpiry && oldExpiry >= lifetime.Expiry)
      lifetime = (oldStart, oldExpiry);
    else if (policy == BuffDurationPolicy.Extend) {
      if (oldExpiry > int.MaxValue - durationTicks)
        return false;
      lifetime = (oldStart, oldExpiry + durationTicks);
    }

    return true;
  }

  private static bool TryApplyChanges(ref Stats stats, ref BuffChanges changes, int buffIndex,
    ReadOnlySpan<BuffSpec> specs, int rank) {
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

    return true;
  }

  private static void Commit(ref Frame frame, EntityRef entity, in Buffs buffs, in BuffChanges changes,
    in Stats stats) {
    if (!frame.Has<Buffs>(entity))
      frame.Add(entity, buffs);
    else
      frame.Get<Buffs>(entity) = buffs;
    if (!frame.Has<BuffChanges>(entity))
      frame.Add(entity, changes);
    else
      frame.Get<BuffChanges>(entity) = changes;
    frame.Get<Stats>(entity) = stats;
  }

  private static void RevertExpired(ref Stats stats, ref Buffs buffs, ref BuffChanges changes, int tick) {
    for (var i = 0; i < Buffs.MaxEntries; i++)
      if (buffs.IsExpired(i, tick))
        RevertBuff(ref stats, ref buffs, ref changes, i);
  }

  private static void RevertBuff(ref Stats stats, ref Buffs buffs, ref BuffChanges changes, int buffIndex) {
    for (var i = 0; i < BuffChanges.MaxEntries; i++)
      if (changes.IsActive(i) && changes.GetBuffIndex(i) == buffIndex)
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
