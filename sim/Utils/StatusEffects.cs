using System;
using System.Collections.Generic;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

public static class StatusEffects {
  private static readonly Comparison<StatusEffect> Comparison = Compare;

  // Replaces the caller's list. Reuse its capacity to avoid allocations during sync.
  public static int Collect(ref Frame frame, EntityRef target, List<StatusEffect> output) {
    ArgumentNullException.ThrowIfNull(frame);
    ArgumentNullException.ThrowIfNull(output);
    output.Clear();
    if (!frame.Entities.IsAlive(target))
      return 0;
    var unitId = UnitLookup.GetUnitId(ref frame, target);
    if (unitId <= 0 || (frame.Has<Health>(target) && !frame.GetReadOnly<Health>(target).IsAlive))
      return 0;

    ReadBuffs(ref frame, target, unitId, output);
    ReadDots(ref frame, unitId, output);
    ReadSnare(ref frame, target, unitId, output);
    ReadSilence(ref frame, target, unitId, output);
    ReadAttackProc(ref frame, target, unitId, output);
    ReadAttackBurst(ref frame, target, unitId, output);
    output.Sort(Comparison);
    return output.Count;
  }

  private static void ReadBuffs(ref Frame frame, EntityRef target, int unitId, List<StatusEffect> output) {
    if (!frame.Has<Buffs>(target) || !frame.Has<BuffChanges>(target))
      return;
    ref readonly var buffs = ref frame.GetReadOnly<Buffs>(target);
    ref readonly var changes = ref frame.GetReadOnly<BuffChanges>(target);
    for (var slot = 0; slot < Buffs.MaxEntries; slot++) {
      if (!buffs.IsActive(slot) || buffs.IsExpired(slot, frame.Tick))
        continue;
      var details = ReadChanges(ref frame, target, in changes, slot);
      if (details.Changes.Count == 0)
        continue;
      var rank = buffs.GetRank(slot);
      output.Add(new StatusEffect {
        Key = new StatusEffectKey(unitId, StatusEffectKind.Buff, slot),
        SourceSkillAssetId = buffs.GetSourceSkillAssetId(slot),
        SourceUnitId = buffs.GetSourceUnitId(slot),
        Rank = rank > 0 ? rank : null,
        Polarity = details.Polarity,
        Timing = new StatusEffectTiming(buffs.GetStartedTick(slot), buffs.GetExpiryTick(slot)),
        Changes = details.Changes
      });
    }
  }

  private static (StatusEffectChanges Changes, EffectPolarity Polarity) ReadChanges(ref Frame frame,
    EntityRef target, in BuffChanges changes, int slot) {
    var result = new StatusEffectChanges();
    var polarity = EffectPolarity.Neutral;
    for (var i = 0; i < BuffChanges.MaxEntries; i++) {
      if (!changes.IsActive(i) || changes.GetBuffIndex(i) != slot)
        continue;
      var change = changes.GetChange(i);
      var changePolarity = EffectPolarity.Neutral;
      if (change.IsStat && (uint)change.Parameter < (uint)StatType.StatCount && frame.Has<Stats>(target))
        changePolarity = change.GetPolarity(StatsController.GetBase(ref frame, target, (StatType)change.Parameter));
      result.Add(new StatusEffectChange(change, changePolarity));
      polarity = CombinePolarity(polarity, changePolarity);
    }
    return (result, polarity);
  }

  private static EffectPolarity CombinePolarity(EffectPolarity current, EffectPolarity next) {
    if (current == EffectPolarity.Neutral)
      return next;
    if (next == EffectPolarity.Neutral || current == next)
      return current;
    return EffectPolarity.Mixed;
  }

  private static void ReadDots(ref Frame frame, int unitId, List<StatusEffect> output) {
    var filter = frame.Filter<DamageOverTime>();
    while (filter.Next(out var effect)) {
      ref readonly var dot = ref frame.GetReadOnly<DamageOverTime>(effect);
      if (!dot.IsBurning || dot.TargetUnitId != unitId || dot.IsExpired(frame.Tick))
        continue;
      output.Add(new StatusEffect {
        Key = new StatusEffectKey(unitId, StatusEffectKind.DamageOverTime, effect.ToId()),
        SourceSkillAssetId = dot.SourceSkillAssetId,
        SourceUnitId = dot.SourceUnitId,
        Polarity = dot.Polarity,
        Timing = new StatusEffectTiming(dot.StartedTick, dot.ExpiryTick),
        Dot = new StatusEffectDot(dot.AccrualPerTick, dot.Pending, dot.IntervalTicks,
          dot.NextPayoutTick, TickMath.DeltaTimeMs(ref frame), DamageType.Magical)
      });
    }
  }

  private static StatusEffect Singleton(int unitId, StatusEffectKind kind, int skillId,
    int startedTick, int expiryTick, EffectPolarity polarity) => new() {
      Key = new StatusEffectKey(unitId, kind, 0),
      SourceSkillAssetId = skillId,
      Polarity = polarity,
      Timing = new StatusEffectTiming(startedTick, expiryTick)
    };

  private static void ReadSnare(ref Frame frame, EntityRef target, int unitId, List<StatusEffect> output) {
    if (!frame.Has<Snare>(target))
      return;
    ref readonly var snare = ref frame.GetReadOnly<Snare>(target);
    if (snare.IsSnared && !snare.IsExpired(frame.Tick))
      output.Add(Singleton(unitId, StatusEffectKind.Snare, snare.SourceId,
        snare.StartedTick, snare.ExpiryTick, snare.Polarity));
  }

  private static void ReadSilence(ref Frame frame, EntityRef target, int unitId, List<StatusEffect> output) {
    if (!frame.Has<Silence>(target))
      return;
    ref readonly var silence = ref frame.GetReadOnly<Silence>(target);
    if (silence.IsSilenced && !silence.IsExpired(frame.Tick))
      output.Add(Singleton(unitId, StatusEffectKind.Silence, silence.SourceId,
        silence.StartedTick, silence.ExpiryTick, silence.Polarity));
  }

  private static void ReadAttackProc(ref Frame frame, EntityRef target, int unitId, List<StatusEffect> output) {
    if (!frame.Has<AttackProc>(target))
      return;
    ref readonly var proc = ref frame.GetReadOnly<AttackProc>(target);
    if (proc.IsArmed && !proc.IsExpired(frame.Tick))
      output.Add(Singleton(unitId, StatusEffectKind.AttackProc, proc.SourceId,
        proc.StartedTick, proc.ExpiryTick, proc.Polarity)
        with { AttackDamageMultiplier = proc.DamageMultiplier });
  }

  private static void ReadAttackBurst(ref Frame frame, EntityRef target, int unitId, List<StatusEffect> output) {
    if (!frame.Has<AttackBurst>(target))
      return;
    ref readonly var burst = ref frame.GetReadOnly<AttackBurst>(target);
    if (burst.IsQueued && !burst.IsExpired(frame.Tick))
      output.Add(Singleton(unitId, StatusEffectKind.AttackBurst, burst.SourceId,
        burst.StartedTick, burst.ExpiryTick, burst.Polarity)
        with { Burst = new StatusEffectBurst(burst.Remaining, burst.DelayTicks) });
  }

  private static int Compare(StatusEffect left, StatusEffect right) {
    var result = ((int)left.Kind).CompareTo((int)right.Kind);
    if (result != 0)
      return result;
    result = left.SourceSkillAssetId.CompareTo(right.SourceSkillAssetId);
    if (result != 0)
      return result;
    result = left.SourceUnitId.CompareTo(right.SourceUnitId);
    return result != 0 ? result : left.Key.InstanceId.CompareTo(right.Key.InstanceId);
  }
}
