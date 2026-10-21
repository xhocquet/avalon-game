using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using Xunit;

namespace Meesles.Avalon.Sim.Tests;

public partial class StatusEffectsQueryTests {
  private const int BuffSkill = AssetIds.SkillCrystalGiantSecondary;
  private const int DotSkill = AssetIds.SkillHairyWizardSecondary;

  [Fact]
  public void MixedEffects_ProduceOneEntryPerInstance_WithTypedDetailsAndAttribution() {
    var harness = SimHarness.CreateInitialized();
    var target = SpawnTarget(harness);
    AddMixedEffects(harness, target);
    var rows = Read(harness, target);
    rows.Select(e => e.Kind).Should().Equal(
      StatusEffectKind.Buff, StatusEffectKind.DamageOverTime, StatusEffectKind.Snare,
      StatusEffectKind.Silence, StatusEffectKind.AttackProc, StatusEffectKind.AttackBurst);
    rows.Select(e => e.Key).Distinct().Should().HaveCount(6);
    rows.Should().OnlyContain(e => e.TargetUnitId == UnitId(harness, target));

    var buff = rows[0];
    buff.SourceSkillAssetId.Should().Be(BuffSkill);
    buff.SourceUnitId.Should().Be(UnitId(harness, harness.FindHero(1)));
    buff.Rank.Should().Be(2);
    buff.Polarity.Should().Be(EffectPolarity.Buff);
    buff.Changes.Count.Should().Be(2);
    buff.Changes[0].Change.Should().Be(BuffChange.Stat(StatType.Armor, BuffMode.Percent, FP64.Half));
    buff.Changes[1].Change.Should().Be(BuffChange.Stat(StatType.MagicResist, BuffMode.Flat, FP64.FromInt(10)));
    buff.Timing.Should().Be(new StatusEffectTiming(0, 100));
    buff.Dot.Should().BeNull();
    buff.AttackDamageMultiplier.Should().BeNull();
    buff.Burst.Should().BeNull();

    var dot = rows[1];
    dot.SourceSkillAssetId.Should().Be(DotSkill);
    dot.SourceUnitId.Should().Be(UnitId(harness, harness.FindHero(2)));
    dot.Polarity.Should().Be(EffectPolarity.Debuff);
    dot.Timing.Should().Be(new StatusEffectTiming(0, 100));
    dot.Rank.Should().BeNull();
    dot.Changes.Count.Should().Be(0);
    dot.Dot.Should().NotBeNull();
    var details = dot.Dot.Value;
    details.AccrualPerTick.Should().Be(FP64.FromInt(40) * FP64.FromInt(SimHarness.DefaultDeltaTimeMs) / FP64.FromInt(1000));
    details.Pending.Should().Be(FP64.Zero);
    details.TickIntervalMs.Should().Be(SimHarness.DefaultDeltaTimeMs);
    details.NextPayoutTick.Should().Be(details.IntervalTicks);
    details.DamageType.Should().Be(DamageType.Magical);
    details.DamagePerSecond.ToFloat().Should().Be(40f);

    rows[2].SourceSkillAssetId.Should().Be(AssetIds.SkillCrystalGiantUltimate);
    rows[3].SourceSkillAssetId.Should().Be(AssetIds.SkillPickleKnightUltimate);
    rows[4].SourceSkillAssetId.Should().Be(AssetIds.SkillCrystalGiantPrimary);
    rows[5].SourceSkillAssetId.Should().Be(AssetIds.SkillPickleKnightSecondary);
    rows[4].AttackDamageMultiplier.Should().Be(FP64.FromInt(2));
    rows[5].Burst.Should().Be(new StatusEffectBurst(3, 2));
    foreach (var row in rows.Skip(2)) {
      row.SourceUnitId.Should().Be(0);
      row.Rank.Should().BeNull();
      row.Timing.StartedTick.Should().Be(0);
      row.Timing.DurationTicks.Should().Be(100);
      row.Timing.GetRemainingFraction(0).Should().Be(FP64.One);
      row.Timing.GetRemainingTicks(0).Should().Be(100);
    }
  }

  [Fact]
  public void Desperation_IsOneMixedEntry_AndCleanseUpdatesOnlyItsRemainingChanges() {
    var harness = SimHarness.CreateInitialized();
    var target = SpawnTarget(harness);
    var frame = harness.Frame;
    var skill = harness.AssetRegistry.Get<SkillAsset>(AssetIds.SkillSkinwalkerUltimate);
    BuffsController.ApplySkill(ref frame, target, skill, 1, UnitId(harness, harness.FindHero(1))).Should().BeTrue();
    var before = Read(harness, target).Should().ContainSingle().Subject;
    before.Polarity.Should().Be(EffectPolarity.Mixed);
    before.Changes.Count.Should().Be(5);
    var beneficial = 0;
    var harmful = 0;
    foreach (var change in before.Changes) {
      if (change.Polarity == EffectPolarity.Buff) beneficial++;
      if (change.Polarity == EffectPolarity.Debuff) harmful++;
    }
    beneficial.Should().Be(3);
    harmful.Should().Be(2);

    BuffsController.ClearHarmful(ref frame, target).Should().Be(2);
    var after = Read(harness, target).Should().ContainSingle().Subject;
    after.Key.Should().Be(before.Key);
    after.Timing.Should().Be(before.Timing);
    after.Changes.Count.Should().Be(3);
    after.Polarity.Should().Be(EffectPolarity.Buff);
    foreach (var change in after.Changes)
      change.Polarity.Should().Be(EffectPolarity.Buff);
    before.Changes.Count.Should().Be(5, "previous query entries are independent value snapshots");
  }

  [Theory]
  [InlineData(StatType.Armor, 0, BuffMode.Percent, 1, 2, EffectPolarity.Neutral)]
  [InlineData(StatType.Armor, -50, BuffMode.Percent, 1, 2, EffectPolarity.Debuff)]
  [InlineData(StatType.Armor, -50, BuffMode.Percent, -1, 2, EffectPolarity.Buff)]
  [InlineData(StatType.MoveSpeed, 10, BuffMode.Percent, -2, 1, EffectPolarity.Debuff)]
  [InlineData(StatType.AttackWindup, 1, BuffMode.Flat, -1, 4, EffectPolarity.Buff)]
  [InlineData(StatType.GameplayRadius, 1, BuffMode.Flat, 1, 4, EffectPolarity.Debuff)]
  public void Polarity_UsesTheCurrentBaseAndSharedStatRules(StatType stat, int baseline, BuffMode mode,
    int numerator, int denominator, EffectPolarity expected) {
    var harness = SimHarness.CreateInitialized();
    var target = SpawnTarget(harness);
    var frame = harness.Frame;
    StatsController.SetBase(ref frame, target, stat, FP64.FromInt(baseline));
    var magnitude = FP64.FromInt(numerator) / FP64.FromInt(denominator);
    ApplyBuff(harness, target, [new(stat, mode, magnitude, FP64.Zero)]);
    var row = Read(harness, target).Should().ContainSingle().Subject;
    row.Polarity.Should().Be(expected);
    row.Changes[0].Polarity.Should().Be(expected);
    row.Changes[0].Change.Magnitude.Should().Be(magnitude);
  }

  [Fact]
  public void FailedWholeBuffApplication_DoesNotProduceAPartialEntry() {
    var harness = SimHarness.CreateInitialized();
    var target = SpawnTarget(harness);
    var frame = harness.Frame;
    var desperation = harness.AssetRegistry.Get<SkillAsset>(AssetIds.SkillSkinwalkerUltimate);
    var sprint = harness.AssetRegistry.Get<SkillAsset>(AssetIds.SkillSkinwalkerPrimary);
    BuffsController.ApplySkill(ref frame, target, desperation, 1).Should().BeTrue();
    var before = Read(harness, target);
    BuffsController.ApplySkill(ref frame, target, sprint, 1).Should().BeFalse();
    AssertSameRows(before, Read(harness, target));
    before.Should().ContainSingle(e => e.Changes.Count == 5 && e.Polarity == EffectPolarity.Mixed);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public void NeutralChanges_DoNotMaskOtherPolarities_RegardlessOfChangeOrder(bool reverse) {
    var harness = SimHarness.CreateInitialized();
    var target = SpawnTarget(harness);
    var frame = harness.Frame;
    StatsController.SetBase(ref frame, target, StatType.Armor, FP64.Zero);
    var neutral = new BuffSpec(StatType.Armor, BuffMode.Percent, FP64.Half, FP64.Zero);
    var beneficial = new BuffSpec(StatType.MoveSpeed, BuffMode.Flat, FP64.One, FP64.Zero);
    ApplyBuff(harness, target, reverse ? [beneficial, neutral] : [neutral, beneficial]);

    var row = Read(harness, target).Should().ContainSingle().Subject;
    row.Polarity.Should().Be(EffectPolarity.Buff);
    row.Changes.Count.Should().Be(2);
    row.Changes[reverse ? 1 : 0].Polarity.Should().Be(EffectPolarity.Neutral);
  }

  [Fact]
  public void SeparateBuffs_DoNotLeakChangesIntoEachOthersDetails_AfterSlotReuse() {
    var harness = SimHarness.CreateInitialized();
    var target = SpawnTarget(harness);
    ApplyBuff(harness, target, [new(StatType.Armor, BuffMode.Flat, FP64.One, FP64.Zero)]);
    ApplyBuff(harness, target, [new(StatType.MoveSpeed, BuffMode.Flat, -FP64.One, FP64.Zero)], skillId: DotSkill);
    var before = Read(harness, target);
    before.Should().HaveCount(2);
    foreach (var row in before)
      row.Changes.Count.Should().Be(1);
    before.Single(e => e.SourceSkillAssetId == BuffSkill).Changes[0].Change.Parameter.Should().Be((int)StatType.Armor);
    before.Single(e => e.SourceSkillAssetId == DotSkill).Changes[0].Change.Parameter.Should().Be((int)StatType.MoveSpeed);

    var frame = harness.Frame;
    BuffsController.ClearHarmful(ref frame, target).Should().Be(1);
    ApplyBuff(harness, target, [new(StatType.MagicResist, BuffMode.Flat, FP64.One, FP64.Zero)], skillId: DotSkill);
    var after = Read(harness, target).Single(e => e.SourceSkillAssetId == DotSkill);
    after.Changes.Count.Should().Be(1);
    after.Changes[0].Change.Parameter.Should().Be((int)StatType.MagicResist);
    after.Polarity.Should().Be(EffectPolarity.Buff);
    Assert.Throws<ArgumentOutOfRangeException>(() => after.Changes[1]);
  }

  [Fact]
  public void ExpiredEffects_AreHiddenBeforeTheTimerPass_WithoutMutatingTheirState() {
    var harness = SimHarness.CreateInitialized();
    var target = SpawnTarget(harness);
    AddMixedEffects(harness, target, 5);
    AdvanceTo(harness, 5);
    var frame = harness.Frame;
    frame.GetReadOnly<Buffs>(target).IsActive(0).Should().BeTrue();
    frame.GetReadOnly<AttackProc>(target).IsArmed.Should().BeTrue();
    harness.Count<DamageOverTime>().Should().Be(1);
    var hash = harness.StateHash;

    Read(harness, target).Should().BeEmpty();
    harness.StateHash.Should().Be(hash);
    harness.Count<DamageOverTime>().Should().Be(1);
  }

  [Fact]
  public void ClearedAndSpentEffects_AreHidden_EvenWithRetainedComponents() {
    var harness = SimHarness.CreateInitialized();
    var target = SpawnTarget(harness);
    AddMixedEffects(harness, target);
    var frame = harness.Frame;
    BuffsController.ClearAll(ref frame, target);
    DamageOverTimeController.Clear(ref frame, target);
    SnareController.Clear(ref frame, target);
    SilenceController.Clear(ref frame, target);
    EmpoweredAttackController.Clear(ref frame, target);
    frame.Get<AttackBurst>(target).Remaining = 0;

    Read(harness, target).Should().BeEmpty();
    frame.Has<Buffs>(target).Should().BeTrue();
    frame.GetReadOnly<AttackBurst>(target).SourceId.Should().NotBe(0);
    harness.Count<DamageOverTime>().Should().Be(1);
  }

  [Fact]
  public void InvalidDeadAndRecycledTargets_ReturnEmpty_AndClearTheOutputBuffer() {
    var harness = SimHarness.CreateInitialized();
    var target = SpawnTarget(harness);
    AddMixedEffects(harness, target);
    var output = Read(harness, target);
    var frame = harness.Frame;
    frame.Get<Health>(target).Current = FP64.Zero;
    StatusEffects.Collect(ref frame, target, output).Should().Be(0);
    output.Should().BeEmpty();
    StatusEffects.Collect(ref frame, EntityRef.None, output).Should().Be(0);
    StatusEffects.Collect(ref frame, new EntityRef(int.MaxValue, 1), output).Should().Be(0);
    StatusEffects.Collect(ref frame, frame.CreateEntity(), output).Should().Be(0);

    frame.DestroyEntity(target);
    var replacement = SpawnTarget(harness);
    replacement.Index.Should().Be(target.Index);
    replacement.Version.Should().NotBe(target.Version);
    ApplyBuff(harness, replacement, [new(StatType.Armor, BuffMode.Flat, FP64.One, FP64.Zero)]);
    StatusEffects.Collect(ref frame, target, output).Should().Be(0);
    StatusEffects.Collect(ref frame, replacement, output).Should().Be(1);
    output.Single().TargetUnitId.Should().Be(UnitId(harness, replacement));
  }

  [Fact]
  public void QueryIsTargetScoped_AndDoesNotRequireAKnownSkillOrLivingCaster() {
    var harness = SimHarness.CreateInitialized();
    var target = SpawnTarget(harness);
    var other = SpawnTarget(harness);
    const int unknownSkill = 424242;
    ApplyBuff(harness, target, [new(StatType.Armor, BuffMode.Flat, FP64.One, FP64.Zero)], skillId: unknownSkill);
    ApplyBuff(harness, other, [new(StatType.MoveSpeed, BuffMode.Flat, FP64.One, FP64.Zero)]);
    var frame = harness.Frame;
    var source = harness.FindHero(1);
    var caster = UnitId(harness, source);
    DamageOverTimeController.Apply(ref frame, target, source, unknownSkill, FP64.FromInt(10), 100).Should().BeTrue();
    DamageOverTimeController.Apply(ref frame, other, source, DotSkill, FP64.FromInt(10), 100).Should().BeTrue();
    frame.DestroyEntity(source);

    var output = Read(harness, target);
    output.Should().HaveCount(2);
    output.Should().OnlyContain(e => e.SourceSkillAssetId == unknownSkill && e.SourceUnitId == caster);
    StatusEffects.Collect(ref frame, other, output).Should().Be(2);
    output.Should().OnlyContain(e => e.TargetUnitId == UnitId(harness, other));
  }

  [Fact]
  public void SavedEntries_RemainIndependentAfterRefreshBaseChangesAndBufferReuse() {
    var harness = SimHarness.CreateInitialized();
    var target = SpawnTarget(harness);
    ApplyBuff(harness, target, [new(StatType.Armor, BuffMode.Percent, FP64.Half, FP64.Zero)]);
    var output = Read(harness, target);
    var saved = output.Single();
    harness.Tick();
    ApplyBuff(harness, target, [new(StatType.Armor, BuffMode.Percent, FP64.One, FP64.Zero)], 200, sourcePlayer: 2, rank: 2);
    var frame = harness.Frame;
    StatsController.SetBase(ref frame, target, StatType.Armor, FP64.FromInt(-50));
    StatusEffects.Collect(ref frame, target, output);
    var current = output.Single();
    current.Key.Should().Be(saved.Key);
    current.SourceUnitId.Should().NotBe(saved.SourceUnitId);
    current.Rank.Should().Be(2);
    current.Polarity.Should().Be(EffectPolarity.Debuff);
    current.Changes[0].Change.Magnitude.Should().Be(FP64.One);
    saved.Rank.Should().Be(1);
    saved.Polarity.Should().Be(EffectPolarity.Buff);
    saved.Changes[0].Change.Magnitude.Should().Be(FP64.Half);
    saved.Timing.Should().Be(new StatusEffectTiming(0, 100));
  }

  [Fact]
  public void SnapshotEquality_ConsidersEveryChange_NotJustTheFirstInlineElement() {
    var harness = SimHarness.CreateInitialized();
    var target = SpawnTarget(harness);
    ApplyBuff(harness, target, [
      new(StatType.Armor, BuffMode.Flat, FP64.One, FP64.Zero),
      new(StatType.MagicResist, BuffMode.Flat, FP64.FromInt(2), FP64.Zero)
    ]);
    var original = Read(harness, target).Single();
    ApplyBuff(harness, target, [
      new(StatType.Armor, BuffMode.Flat, FP64.One, FP64.Zero),
      new(StatType.MagicResist, BuffMode.Flat, FP64.FromInt(3), FP64.Zero)
    ]);
    var updated = Read(harness, target).Single();
    updated.Key.Should().Be(original.Key);
    updated.Should().NotBe(original);
    updated.Changes.Should().NotBe(original.Changes);

    ApplyBuff(harness, target, [
      new(StatType.Armor, BuffMode.Flat, FP64.One, FP64.Zero),
      new(StatType.MagicResist, BuffMode.Flat, FP64.FromInt(2), FP64.Zero)
    ]);
    var restored = Read(harness, target).Single();
    restored.Should().Be(original);
    restored.GetHashCode().Should().Be(original.GetHashCode());
  }

  [Fact]
  public void ReadingMixedEffects_IsReadOnlyAndAllocatesNothingWithAWarmedBuffer() {
    var harness = SimHarness.CreateInitialized();
    var target = SpawnTarget(harness);
    AddMixedEffects(harness, target);
    var frame = harness.Frame;
    var output = new List<StatusEffect>(64);
    for (var i = 0; i < 100; i++)
      StatusEffects.Collect(ref frame, target, output);
    var hash = harness.StateHash;
    var before = GC.GetAllocatedBytesForCurrentThread();
    for (var i = 0; i < 100; i++)
      StatusEffects.Collect(ref frame, target, output);
    var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

    allocated.Should().Be(0);
    harness.StateHash.Should().Be(hash);
    output.Should().HaveCount(6);
    output.Single(e => e.Kind == StatusEffectKind.DamageOverTime).Dot.Value.Pending.Should().Be(FP64.Zero);
  }

  [Fact]
  public void Timing_ClampsKnownLifetimes_AndDoesNotGuessMissingDurations() {
    var known = new StatusEffectTiming(10, 30);
    known.DurationTicks.Should().Be(20);
    known.GetRemainingTicks(0).Should().Be(20);
    known.GetRemainingFraction(0).Should().Be(FP64.One);
    known.GetRemainingTicks(20).Should().Be(10);
    known.GetRemainingFraction(20).Should().Be(FP64.Half);
    known.GetRemainingFraction(30).Should().Be(FP64.Zero);
    known.GetRemainingTicks(40).Should().Be(0);
    new StatusEffectTiming(30, 30).GetRemainingFraction(30).Should().Be(FP64.Zero);
    var unknown = new StatusEffectTiming(null, 30);
    unknown.GetRemainingTicks(20).Should().Be(10);
    unknown.DurationTicks.Should().BeNull();
    unknown.GetRemainingFraction(20).Should().BeNull();
  }

  private static EntityRef SpawnTarget(SimHarness harness) {
    var frame = harness.Frame;
    var target = frame.CreateEntity();
    frame.Add(target, new UnitIdentity { UnitId = UnitLookup.NextUnitId(ref frame), UnitTypeId = SimulationSetup.MinionUnitTypeId });
    frame.Add(target, new Minion());
    frame.Add(target, new Health(FP64.FromInt(10000)));
    frame.Add(target, Stats.Create().With(StatType.MaxHealth, FP64.FromInt(10000))
      .With(StatType.Armor, FP64.FromInt(100)).With(StatType.MagicResist, FP64.FromInt(100))
      .With(StatType.MoveSpeed, FP64.FromInt(10)).With(StatType.AttackWindup, FP64.One)
      .With(StatType.GameplayRadius, FP64.One));
    return target;
  }

  private static void AddMixedEffects(SimHarness harness, EntityRef target, int duration = 100) {
    ApplyBuff(harness, target, [
      new(StatType.Armor, BuffMode.Percent, FP64.Half, FP64.Zero),
      new(StatType.MagicResist, BuffMode.Flat, FP64.FromInt(10), FP64.Zero)
    ], duration, rank: 2);
    var frame = harness.Frame;
    DamageOverTimeController.Apply(ref frame, target, harness.FindHero(2), DotSkill, FP64.FromInt(40), duration).Should().BeTrue();
    SnareController.Apply(ref frame, target, AssetIds.SkillCrystalGiantUltimate, duration).Should().BeTrue();
    SilenceController.Apply(ref frame, target, AssetIds.SkillPickleKnightUltimate, duration).Should().BeTrue();
    EmpoweredAttackController.Arm(ref frame, target, AssetIds.SkillCrystalGiantPrimary, FP64.FromInt(2), duration).Should().BeTrue();
    BurstAttacksController.Queue(ref frame, target, AssetIds.SkillPickleKnightSecondary, 4, 2, duration).Should().BeTrue();
  }

  private static void ApplyBuff(SimHarness harness, EntityRef target, ReadOnlySpan<BuffSpec> specs,
    int duration = 100, int skillId = BuffSkill, int sourcePlayer = 1, int rank = 1,
    BuffApplicationPolicy? policy = null) {
    var frame = harness.Frame;
    BuffsController.Apply(ref frame, target, skillId, specs, rank, duration,
      UnitId(harness, harness.FindHero(sourcePlayer)), policy).Should().BeTrue();
  }

  private static List<StatusEffect> Read(SimHarness harness, EntityRef target) {
    var frame = harness.Frame;
    var result = new List<StatusEffect>();
    StatusEffects.Collect(ref frame, target, result).Should().Be(result.Count);
    return result;
  }

  private static int UnitId(SimHarness harness, EntityRef unit) =>
    harness.Frame.GetReadOnly<UnitIdentity>(unit).UnitId;

  private static void AdvanceTo(SimHarness harness, int tick) {
    while (harness.Frame.Tick < tick)
      harness.Tick();
  }
}
