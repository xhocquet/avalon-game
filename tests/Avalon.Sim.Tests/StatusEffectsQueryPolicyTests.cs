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
  [Theory]
  [InlineData(StatusEffectKind.Snare)]
  [InlineData(StatusEffectKind.Silence)]
  [InlineData(StatusEffectKind.AttackProc)]
  [InlineData(StatusEffectKind.AttackBurst)]
  public void SingleSlotStatuses_ExposeRealStartTicks_AndResetTimingOnAcceptedReapplication(StatusEffectKind kind) {
    var harness = SimHarness.CreateInitialized();
    var target = SpawnTarget(harness);
    AdvanceTo(harness, 7);
    ApplySingleSlot(harness, target, kind, 20).Should().BeTrue();
    var initial = Read(harness, target).Should().ContainSingle().Subject;
    initial.Timing.Should().Be(new StatusEffectTiming(7, 27));
    initial.Timing.GetRemainingFraction(7).Should().Be(FP64.One);

    AdvanceTo(harness, 12);
    Read(harness, target).Single().Timing.GetRemainingFraction(12)
      .Should().Be(FP64.FromInt(15) / FP64.FromInt(20));
    ApplySingleSlot(harness, target, kind, 20).Should().BeTrue();
    var refreshed = Read(harness, target).Single();
    refreshed.Key.Should().Be(initial.Key);
    refreshed.Timing.Should().Be(new StatusEffectTiming(12, 32));
    refreshed.Timing.GetRemainingFraction(12).Should().Be(FP64.One);

    if (kind is StatusEffectKind.Snare or StatusEffectKind.Silence) {
      AdvanceTo(harness, 13);
      ApplySingleSlot(harness, target, kind, 1).Should().BeFalse();
      Read(harness, target).Single().Timing.Should().Be(refreshed.Timing);
    }
  }

  [Theory]
  [InlineData(StatusEffectKind.Snare)]
  [InlineData(StatusEffectKind.Silence)]
  [InlineData(StatusEffectKind.AttackProc)]
  [InlineData(StatusEffectKind.AttackBurst)]
  public void Rollback_RestoresSingleSlotStartTicksAndRemainingFractions(StatusEffectKind kind) {
    var rollback = RollbackHarness.Create();
    rollback.Advance(5);
    rollback.Advance(1, beforeTick: sim => ApplySingleSlot(sim, sim.FindHero(1), kind, 100));
    var snapshot = Read(rollback.Server, rollback.Server.FindHero(1));
    snapshot.Single().Timing.Should().Be(new StatusEffectTiming(5, 105));
    rollback.MispredictAndRollback(3, null,
      beforeTick: sim => ApplySingleSlot(sim, sim.FindHero(1), kind, 200));
    rollback.InSync.Should().BeTrue();
    AssertSameRows(snapshot, Read(rollback.Client, rollback.Client.FindHero(1)));
    rollback.AdvanceAndCompare(10).Should().BeEmpty();
  }

  [Theory]
  [InlineData(false, true, 1, 2)]
  [InlineData(true, false, 2, 1)]
  [InlineData(true, true, 2, 2)]
  public void OneSkill_CanHaveIndependentBuffAndDotStackingPolicies(bool buffStacks, bool dotStacks,
    int expectedBuffs, int expectedDots) {
    var harness = SimHarness.CreateInitialized();
    var target = SpawnTarget(harness);
    var skill = harness.AssetRegistry.Get<SkillAsset>(DotSkill);
    skill.BuffReapply = (int)(buffStacks ? BuffReapplication.Stack : BuffReapplication.Refresh);
    skill.DotReapply = (int)(dotStacks ? BuffReapplication.Stack : BuffReapplication.Refresh);
    skill.BuffMaxStacks = skill.DotMaxStacks = 2;
    skill.BuffOverflow = skill.DotOverflow = (int)BuffStackOverflow.RefreshOldest;
    for (var i = 0; i < 3; i++) {
      var frame = harness.Frame;
      var caster = UnitId(harness, harness.FindHero(1));
      BuffsController.ApplySkill(ref frame, target, skill, 1, caster).Should().BeTrue();
      DamageOverTimeController.ApplySkill(ref frame, target, caster, skill, 1).Should().BeTrue();
      harness.Tick();
    }

    var rows = Read(harness, target);
    rows.Count(e => e.Kind == StatusEffectKind.Buff).Should().Be(expectedBuffs);
    rows.Count(e => e.Kind == StatusEffectKind.DamageOverTime).Should().Be(expectedDots);
    rows.Select(e => e.Key).Distinct().Should().HaveCount(expectedBuffs + expectedDots);
    rows.Should().OnlyContain(e => e.SourceSkillAssetId == DotSkill && e.Polarity == EffectPolarity.Debuff);
    foreach (var buff in rows.Where(e => e.Kind == StatusEffectKind.Buff))
      buff.Changes.Count.Should().Be(1);
    foreach (var dot in rows.Where(e => e.Kind == StatusEffectKind.DamageOverTime)) {
      dot.Changes.Count.Should().Be(0);
      dot.Dot.Should().NotBeNull();
      dot.Rank.Should().BeNull();
    }
  }

  [Theory]
  [InlineData(BuffIdentity.Skill, 2)]
  [InlineData(BuffIdentity.SkillPerCaster, 4)]
  public void BuffStackIdentityOverrides_ProduceDistinctKeysAndCasterGroups(BuffIdentity identity, int count) {
    var harness = SimHarness.CreateInitialized();
    var target = SpawnTarget(harness);
    var policy = new BuffApplicationPolicy { Identity = identity, Reapplication = BuffReapplication.Stack, MaxStacks = 2 };
    var frame = harness.Frame;
    foreach (var player in new[] { 1, 2 })
      for (var i = 0; i < 2; i++)
        BuffsController.Apply(ref frame, target, BuffSkill,
          [new(StatType.Armor, BuffMode.Flat, FP64.One, FP64.One)], i + 1, 100,
          UnitId(harness, harness.FindHero(player)), policy).Should()
          .Be(player == 1 || identity == BuffIdentity.SkillPerCaster);

    var rows = Read(harness, target);
    rows.Should().HaveCount(count);
    rows.Select(e => e.Key).Distinct().Should().HaveCount(count);
    rows.Select(e => e.SourceUnitId).Distinct().Should().HaveCount(count / 2);
    foreach (var row in rows) {
      row.Changes.Count.Should().Be(1);
      row.Changes[0].Change.Magnitude.Should().Be(FP64.FromInt(row.Rank.Value));
    }
  }

  [Fact]
  public void PerCasterDotStacks_KeepSeparateIdentitiesEvenWhenTheirTimingsMatch() {
    var harness = SimHarness.CreateInitialized();
    var target = SpawnTarget(harness);
    var skill = harness.AssetRegistry.Get<SkillAsset>(DotSkill);
    skill.DotIdentityMode = (int)BuffIdentity.SkillPerCaster;
    skill.DotReapply = (int)BuffReapplication.Stack;
    skill.DotMaxStacks = 2;
    var frame = harness.Frame;
    foreach (var player in new[] { 1, 2 })
      for (var i = 0; i < 2; i++)
        DamageOverTimeController.ApplySkill(ref frame, target, UnitId(harness, harness.FindHero(player)), skill, 1)
          .Should().BeTrue();

    var rows = Read(harness, target);
    rows.Should().HaveCount(4);
    rows.Select(e => e.Key).Distinct().Should().HaveCount(4);
    rows.Select(e => e.Timing).Distinct().Should().ContainSingle();
    foreach (var group in rows.GroupBy(e => e.SourceUnitId))
      group.Should().HaveCount(2);
  }

  [Theory]
  [InlineData(StatusEffectKind.Buff, BuffDurationPolicy.Restart, 10, 5, 15)]
  [InlineData(StatusEffectKind.Buff, BuffDurationPolicy.KeepLaterExpiry, 10, 0, 20)]
  [InlineData(StatusEffectKind.Buff, BuffDurationPolicy.KeepLaterExpiry, 15, 0, 20)]
  [InlineData(StatusEffectKind.Buff, BuffDurationPolicy.KeepLaterExpiry, 30, 5, 35)]
  [InlineData(StatusEffectKind.Buff, BuffDurationPolicy.Extend, 10, 0, 30)]
  [InlineData(StatusEffectKind.DamageOverTime, BuffDurationPolicy.Restart, 10, 5, 15)]
  [InlineData(StatusEffectKind.DamageOverTime, BuffDurationPolicy.KeepLaterExpiry, 10, 0, 20)]
  [InlineData(StatusEffectKind.DamageOverTime, BuffDurationPolicy.KeepLaterExpiry, 15, 0, 20)]
  [InlineData(StatusEffectKind.DamageOverTime, BuffDurationPolicy.KeepLaterExpiry, 30, 5, 35)]
  [InlineData(StatusEffectKind.DamageOverTime, BuffDurationPolicy.Extend, 10, 0, 30)]
  public void TimingOverrides_AreReadFromStoredState_NotReconstructedFromAssets(StatusEffectKind kind,
    BuffDurationPolicy timing, int incomingDuration, int start, int expiry) {
    var harness = SimHarness.CreateInitialized();
    var target = SpawnTarget(harness);
    var policy = BuffApplicationPolicy.Default;
    policy.Duration = timing;
    ApplyForQuery(harness, target, kind, policy, 1, 20).Should().BeTrue();
    var key = Read(harness, target).Single().Key;
    AdvanceTo(harness, 5);
    ApplyForQuery(harness, target, kind, policy, 2, incomingDuration).Should().BeTrue();

    var row = Read(harness, target).Should().ContainSingle().Subject;
    row.Key.Should().Be(key);
    row.Timing.Should().Be(new StatusEffectTiming(start, expiry));
    row.Timing.DurationTicks.Should().Be(expiry - start);
    row.Timing.GetRemainingTicks(5).Should().Be(expiry - 5);
    row.Timing.GetRemainingFraction(5).Should().Be(FP64.FromInt(expiry - 5) / FP64.FromInt(expiry - start));
  }

  [Theory]
  [InlineData(StatusEffectKind.Buff)]
  [InlineData(StatusEffectKind.DamageOverTime)]
  public void IgnoredApplications_DoNotChangeTheReadModel(StatusEffectKind kind) {
    var harness = SimHarness.CreateInitialized();
    var target = SpawnTarget(harness);
    var policy = BuffApplicationPolicy.Default;
    policy.Reapplication = BuffReapplication.Ignore;
    ApplyForQuery(harness, target, kind, policy, 1, 100).Should().BeTrue();
    AdvanceTo(harness, 5);
    var before = Read(harness, target);
    ApplyForQuery(harness, target, kind, policy, 3, 200, 2).Should().BeFalse();
    AssertSameRows(before, Read(harness, target));
  }

  [Theory]
  [InlineData(StatusEffectKind.Buff, BuffStackOverflow.Reject)]
  [InlineData(StatusEffectKind.Buff, BuffStackOverflow.RefreshOldest)]
  [InlineData(StatusEffectKind.Buff, BuffStackOverflow.ReplaceWeakest)]
  [InlineData(StatusEffectKind.DamageOverTime, BuffStackOverflow.Reject)]
  [InlineData(StatusEffectKind.DamageOverTime, BuffStackOverflow.RefreshOldest)]
  [InlineData(StatusEffectKind.DamageOverTime, BuffStackOverflow.ReplaceWeakest)]
  public void OverflowOverrides_ExposeOnlyTheResultingActiveInstances(StatusEffectKind kind, BuffStackOverflow overflow) {
    var harness = SimHarness.CreateInitialized();
    var target = SpawnTarget(harness);
    var policy = new BuffApplicationPolicy {
      Reapplication = BuffReapplication.Stack,
      MaxStacks = 2,
      Duration = BuffDurationPolicy.Extend,
      Overflow = overflow
    };
    ApplyForQuery(harness, target, kind, policy, 1, 20).Should().BeTrue();
    harness.Tick();
    ApplyForQuery(harness, target, kind, policy, 2, 20).Should().BeTrue();
    harness.Tick();
    var before = Read(harness, target);
    ApplyForQuery(harness, target, kind, policy, 3, 10, 2).Should().Be(overflow != BuffStackOverflow.Reject);
    var rows = Read(harness, target);
    rows.Should().HaveCount(2);
    if (overflow == BuffStackOverflow.Reject) {
      AssertSameRows(before, rows);
      return;
    }

    var updated = rows.Single(e => e.SourceUnitId == UnitId(harness, harness.FindHero(2)));
    updated.Timing.ExpiryTick.Should().Be(overflow == BuffStackOverflow.ReplaceWeakest ? 12 : 30);
    if (kind == StatusEffectKind.DamageOverTime && overflow == BuffStackOverflow.ReplaceWeakest) {
      rows.Count(e => before.Any(old => old.Key == e.Key)).Should().Be(1);
      harness.Count<DamageOverTime>().Should().Be(3, "the retired stack awaits its final payout, but is not rendered");
    }
    else
      rows.Select(e => e.Key).Should().BeEquivalentTo(before.Select(e => e.Key));
  }

  [Fact]
  public void SmallOutputBuffers_GrowWithoutTruncatingStacksOrOtherEffectKinds() {
    var harness = SimHarness.CreateInitialized();
    var target = SpawnTarget(harness);
    var policy = new BuffApplicationPolicy { Reapplication = BuffReapplication.Stack, MaxStacks = 20 };
    var frame = harness.Frame;
    for (var i = 0; i < BuffChanges.MaxEntries; i++)
      ApplyBuff(harness, target, [new(StatType.Armor, BuffMode.Flat, FP64.One, FP64.Zero)], policy: policy);
    for (var i = 0; i < 20; i++)
      DamageOverTimeController.Apply(ref frame, target, harness.FindHero(1), DotSkill, FP64.One, 100, policy)
        .Should().BeTrue();
    SnareController.Apply(ref frame, target, BuffSkill, 100);
    SilenceController.Apply(ref frame, target, BuffSkill, 100);
    EmpoweredAttackController.Arm(ref frame, target, BuffSkill, FP64.FromInt(2), 100);
    BurstAttacksController.Queue(ref frame, target, BuffSkill, 3, 2, 100);
    var output = new List<StatusEffect>(1);

    StatusEffects.Collect(ref frame, target, output).Should().Be(30);
    output.Select(e => e.Key).Distinct().Should().HaveCount(30);
    output.Count(e => e.Kind == StatusEffectKind.Buff).Should().Be(6);
    output.Count(e => e.Kind == StatusEffectKind.DamageOverTime).Should().Be(20);
  }

  [Fact]
  public void Ordering_IsCanonical_EvenAfterDotStorageCompacts() {
    var harness = SimHarness.CreateInitialized();
    var target = SpawnTarget(harness);
    ApplyBuff(harness, target, [new(StatType.Armor, BuffMode.Flat, FP64.One, FP64.Zero)], skillId: 600);
    ApplyBuff(harness, target, [new(StatType.MagicResist, BuffMode.Flat, FP64.One, FP64.Zero)], skillId: 400);
    var policy = new BuffApplicationPolicy { Reapplication = BuffReapplication.Stack, MaxStacks = 3 };
    var frame = harness.Frame;
    for (var i = 0; i < 3; i++)
      DamageOverTimeController.Apply(ref frame, target, harness.FindHero(1), DotSkill, FP64.One, 100, policy)
        .Should().BeTrue();
    var before = Read(harness, target);
    before.Where(e => e.Kind == StatusEffectKind.Buff).Select(e => e.SourceSkillAssetId).Should().Equal(400, 600);
    var firstDotKey = before.First(e => e.Kind == StatusEffectKind.DamageOverTime).Key;
    var entities = new List<EntityRef>();
    var filter = frame.Filter<DamageOverTime>();
    while (filter.Next(out var effect))
      entities.Add(effect);
    frame.DestroyEntity(entities.Single(e => e.ToId() == firstDotKey.InstanceId));

    Read(harness, target).Select(e => e.Key).Should().Equal(before.Where(e => e.Key != firstDotKey).Select(e => e.Key));
  }

  [Fact]
  public void RollbackAndRequery_DoNotLeakDiscardedEffectsOrCachedFrameState() {
    var rollback = RollbackHarness.Create();
    rollback.Advance(5, beforeTick: ApplyQueryScenario);
    var snapshot = Read(rollback.Server, rollback.Server.FindHero(1));
    rollback.MispredictAndRollback(8, null, beforeTick: sim => {
      var frame = sim.Frame;
      var target = sim.FindHero(1);
      BuffsController.ClearAll(ref frame, target);
      DamageOverTimeController.Clear(ref frame, target);
      ApplyBuff(sim, target, [new(StatType.MoveSpeed, BuffMode.Flat, -FP64.One, FP64.Zero)], skillId: DotSkill);
      DamageOverTimeController.Apply(ref frame, target, sim.FindHero(2), BuffSkill, FP64.FromInt(100), 30);
      Read(sim, target).Should().NotBeEmpty();
    });
    rollback.InSync.Should().BeTrue();
    AssertSameRows(snapshot, Read(rollback.Client, rollback.Client.FindHero(1)));
    rollback.AdvanceAndCompare(30, beforeTick: ApplyQueryScenario).Should().BeEmpty();
    AssertSameRows(Read(rollback.Server, rollback.Server.FindHero(1)), Read(rollback.Client, rollback.Client.FindHero(1)));
  }

  private static void ApplyQueryScenario(SimHarness harness) {
    var target = harness.FindHero(1);
    var policy = new BuffApplicationPolicy {
      Reapplication = BuffReapplication.Stack,
      MaxStacks = 2,
      Overflow = BuffStackOverflow.RefreshOldest
    };
    ApplyBuff(harness, target, [new(StatType.Armor, BuffMode.Flat, FP64.One, FP64.One)],
      rank: harness.Frame.Tick % 3 + 1, policy: policy);
    var frame = harness.Frame;
    DamageOverTimeController.Apply(ref frame, target, harness.FindHero(2), DotSkill, FP64.FromInt(10), 100, policy);
    Read(harness, target).Should().HaveCount(frame.Tick == 0 ? 2 : 4);
  }

  private static bool ApplyForQuery(SimHarness harness, EntityRef target, StatusEffectKind kind,
    BuffApplicationPolicy policy, int rank, int duration, int sourcePlayer = 1) {
    var frame = harness.Frame;
    var source = harness.FindHero(sourcePlayer);
    return kind == StatusEffectKind.Buff
      ? BuffsController.Apply(ref frame, target, BuffSkill,
        [new(StatType.Armor, BuffMode.Flat, FP64.One, FP64.One)], rank, duration, UnitId(harness, source), policy)
      : DamageOverTimeController.Apply(ref frame, target, source, DotSkill, FP64.FromInt(rank * 40), duration, policy);
  }

  private static bool ApplySingleSlot(SimHarness harness, EntityRef target, StatusEffectKind kind, int duration) {
    var frame = harness.Frame;
    return kind switch {
      StatusEffectKind.Snare => SnareController.Apply(ref frame, target, AssetIds.SkillCrystalGiantUltimate, duration),
      StatusEffectKind.Silence => SilenceController.Apply(ref frame, target, AssetIds.SkillPickleKnightUltimate, duration),
      StatusEffectKind.AttackProc => EmpoweredAttackController.Arm(ref frame, target, AssetIds.SkillCrystalGiantPrimary,
        FP64.FromInt(2), duration),
      StatusEffectKind.AttackBurst => BurstAttacksController.Queue(ref frame, target,
        AssetIds.SkillPickleKnightSecondary, 4, 2, duration),
      _ => throw new System.ArgumentOutOfRangeException(nameof(kind))
    };
  }

  private static void AssertSameRows(IReadOnlyList<StatusEffect> expected, IReadOnlyList<StatusEffect> actual) {
    actual.Count.Should().Be(expected.Count);
    for (var i = 0; i < expected.Count; i++) {
      actual[i].Key.Should().Be(expected[i].Key);
      actual[i].SourceSkillAssetId.Should().Be(expected[i].SourceSkillAssetId);
      actual[i].SourceUnitId.Should().Be(expected[i].SourceUnitId);
      actual[i].Rank.Should().Be(expected[i].Rank);
      actual[i].Polarity.Should().Be(expected[i].Polarity);
      actual[i].Timing.Should().Be(expected[i].Timing);
      actual[i].Dot.Should().Be(expected[i].Dot);
      actual[i].AttackDamageMultiplier.Should().Be(expected[i].AttackDamageMultiplier);
      actual[i].Burst.Should().Be(expected[i].Burst);
      actual[i].Changes.Count.Should().Be(expected[i].Changes.Count);
      for (var j = 0; j < expected[i].Changes.Count; j++)
        actual[i].Changes[j].Should().Be(expected[i].Changes[j]);
    }
  }
}
