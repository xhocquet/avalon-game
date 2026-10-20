using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.ECS.Json;
using Xunit;

namespace Meesles.Avalon.Sim.Tests;

public class BuffPolicyTests {
  private const int SkillId = AssetIds.SkillCrystalGiantSecondary;

  [Fact]
  public void AuthoredPolicy_SurvivesJsonAndBinaryAssetSerialization() {
    WarmupRegistry.RunAll();
    var bytes = DataAssetJsonConverter.ConvertMixedJsonToBytes("""
      [{
        "$type": "Meesles.Avalon.Sim.Assets.SkillAsset",
        "AssetId": 509,
        "BuffIdentityMode": 1,
        "BuffReapply": 1,
        "BuffDurationMode": 2,
        "BuffMaxStacks": 3,
        "BuffOverflow": 2
      }]
      """);
    var skill = (SkillAsset)DataAssetReader.LoadMixedCollectionFromBytes(bytes).Single();
    skill.BuffPolicy.Should().BeEquivalentTo(new BuffApplicationPolicy {
      Identity = BuffIdentity.SkillPerCaster,
      Reapplication = BuffReapplication.Stack,
      Duration = BuffDurationPolicy.Extend,
      MaxStacks = 3,
      Overflow = BuffStackOverflow.ReplaceWeakest
    });
  }

  [Fact]
  public void AuthoredDefaults_RefreshAcrossCasters_AndRetainLatestCasterAndRank() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var target = harness.FindHero(1);
    var skill = harness.AssetRegistry.Get<SkillAsset>(SkillId);
    skill.BuffPolicy.Should().BeEquivalentTo(BuffApplicationPolicy.Default);
    var firstCaster = UnitLookup.GetUnitId(ref frame, target);
    var secondCaster = UnitLookup.GetUnitId(ref frame, harness.FindHero(2));

    BuffsController.ApplySkill(ref frame, target, skill, 1, firstCaster).Should().BeTrue();
    harness.Tick();
    frame = harness.Frame;
    BuffsController.ApplySkill(ref frame, target, skill, 2, secondCaster).Should().BeTrue();
    var buff = Entries(harness).Should().ContainSingle().Subject;
    buff.Caster.Should().Be(secondCaster);
    buff.Rank.Should().Be(2);
    buff.Start.Should().Be(frame.Tick);
    BuffTestQueries.StatChanges(frame, target).Should().HaveCount(2);
  }

  [Fact]
  public void PerCasterIdentity_GivesEachCasterAnIndependentStackCap() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var target = harness.FindHero(1);
    var skill = harness.AssetRegistry.Get<SkillAsset>(SkillId);
    skill.BuffIdentityMode = (int)BuffIdentity.SkillPerCaster;
    skill.BuffReapply = (int)BuffReapplication.Stack;
    var first = UnitLookup.GetUnitId(ref frame, target);
    var second = UnitLookup.GetUnitId(ref frame, harness.FindHero(2));

    BuffsController.ApplySkill(ref frame, target, skill, 1, first).Should().BeTrue();
    BuffsController.ApplySkill(ref frame, target, skill, 1, first).Should().BeFalse();
    BuffsController.ApplySkill(ref frame, target, skill, 1, second).Should().BeTrue();
    BuffsController.ApplySkill(ref frame, target, skill, 1).Should().BeFalse();
    Entries(harness).Select(e => e.Caster).Should().BeEquivalentTo([first, second]);
  }

  [Fact]
  public void Ignore_PreservesStatsTimingAndAttribution() {
    var harness = SimHarness.CreateInitialized();
    var policy = BuffApplicationPolicy.Default;
    policy.Reapplication = BuffReapplication.Ignore;
    Apply(harness, policy, 1, 20, 1).Should().BeTrue();
    harness.Tick();
    var hash = harness.StateHash;
    Apply(harness, policy, 3, 40, 2).Should().BeFalse();
    harness.StateHash.Should().Be(hash);
  }

  [Theory]
  [InlineData(BuffDurationPolicy.Restart, 10, 5, 15)]
  [InlineData(BuffDurationPolicy.KeepLaterExpiry, 10, 0, 20)]
  [InlineData(BuffDurationPolicy.KeepLaterExpiry, 15, 0, 20)]
  [InlineData(BuffDurationPolicy.KeepLaterExpiry, 30, 5, 35)]
  [InlineData(BuffDurationPolicy.Extend, 10, 0, 30)]
  public void Refresh_AppliesTheSelectedDurationRule(BuffDurationPolicy timing, int incomingDuration,
    int expectedStart, int expectedExpiry) {
    var harness = SimHarness.CreateInitialized();
    var policy = BuffApplicationPolicy.Default;
    policy.Duration = timing;
    Apply(harness, policy, 1, 20).Should().BeTrue();
    AdvanceTo(harness, 5);
    Apply(harness, policy, 2, incomingDuration).Should().BeTrue();

    var buff = Entries(harness).Should().ContainSingle().Subject;
    buff.Start.Should().Be(expectedStart);
    buff.Expiry.Should().Be(expectedExpiry);
    buff.Rank.Should().Be(2);
  }

  [Fact]
  public void Stacks_ExpireIndependently_AndReverseOnlyTheirOwnChanges() {
    var harness = SimHarness.CreateInitialized();
    var policy = BuffApplicationPolicy.Default;
    policy.Reapplication = BuffReapplication.Stack;
    policy.MaxStacks = 2;
    var target = harness.FindHero(1);
    var baseline = harness.Frame.GetReadOnly<Stats>(target).Armor;
    Apply(harness, policy, 1, 2).Should().BeTrue();
    harness.Tick();
    Apply(harness, policy, 2, 10).Should().BeTrue();
    Entries(harness).Should().HaveCount(2);
    harness.Frame.GetReadOnly<Stats>(target).Armor.Should().Be(baseline + FP64.FromInt(3));

    AdvanceTo(harness, 3);
    Entries(harness).Should().ContainSingle(e => e.Rank == 2);
    harness.Frame.GetReadOnly<Stats>(target).Armor.Should().Be(baseline + FP64.FromInt(2));
    AdvanceTo(harness, 12);
    Entries(harness).Should().BeEmpty();
    harness.Frame.GetReadOnly<Stats>(target).Armor.Should().Be(baseline);
  }

  [Theory]
  [InlineData(BuffStackOverflow.Reject)]
  [InlineData(BuffStackOverflow.RefreshOldest)]
  [InlineData(BuffStackOverflow.ReplaceWeakest)]
  public void StackCap_AppliesTheSelectedOverflowRule(BuffStackOverflow overflow) {
    var harness = SimHarness.CreateInitialized();
    var policy = BuffApplicationPolicy.Default;
    policy.Reapplication = BuffReapplication.Stack;
    policy.Duration = BuffDurationPolicy.Extend;
    policy.MaxStacks = 2;
    policy.Overflow = overflow;
    Apply(harness, policy, 3, 20).Should().BeTrue();
    harness.Tick();
    Apply(harness, policy, 1, 20).Should().BeTrue();
    harness.Tick();
    var before = harness.StateHash;
    var applied = Apply(harness, policy, 2, 10);

    if (overflow == BuffStackOverflow.Reject) {
      applied.Should().BeFalse();
      harness.StateHash.Should().Be(before);
    }
    else {
      applied.Should().BeTrue();
      var entries = Entries(harness);
      entries.Should().HaveCount(2);
      var updated = entries.Single(e => e.Rank == 2);
      if (overflow == BuffStackOverflow.RefreshOldest) {
        entries.Should().Contain(e => e.Rank == 1);
        updated.Start.Should().Be(0);
        updated.Expiry.Should().Be(30);
      }
      else {
        entries.Should().Contain(e => e.Rank == 3);
        updated.Start.Should().Be(2);
        updated.Expiry.Should().Be(12);
      }
    }
  }

  [Fact]
  public void ExpiredInstances_DoNotCountAgainstTheCapBeforeTheTimerPass() {
    var harness = SimHarness.CreateInitialized();
    var policy = BuffApplicationPolicy.Default;
    policy.Reapplication = BuffReapplication.Ignore;
    Apply(harness, policy, 1, 1).Should().BeTrue();
    harness.Tick();
    Apply(harness, policy, 2, 10).Should().BeTrue();
    Entries(harness).Should().ContainSingle(e => e.Rank == 2 && e.Start == 1);
  }

  [Fact]
  public void CapacityFailure_DoesNotEvictAnotherSkillOrPartiallyAddAStack() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var target = harness.FindHero(1);
    var skill = harness.AssetRegistry.Get<SkillAsset>(SkillId);
    skill.BuffReapply = (int)BuffReapplication.Stack;
    skill.BuffMaxStacks = 3;
    BuffsController.ApplySkill(ref frame, target, skill, 1).Should().BeTrue();
    BuffsController.ApplySkill(ref frame, target, skill, 1).Should().BeTrue();
    var sprint = harness.AssetRegistry.Get<SkillAsset>(AssetIds.SkillSkinwalkerPrimary);
    BuffsController.ApplySkill(ref frame, target, sprint, 1).Should().BeTrue();
    var hash = harness.StateHash;

    BuffsController.ApplySkill(ref frame, target, skill, 1).Should().BeFalse();
    harness.StateHash.Should().Be(hash);
  }

  [Fact]
  public void Rollback_RestoresStackLifetimesRanksAndCasterAttribution() {
    var rollback = RollbackHarness.Create();
    rollback.Advance(10, beforeTick: ApplyAuthoritative);
    Entries(rollback.Server).Should().HaveCount(4);
    rollback.MispredictAndRollback(8, null, beforeTick: sim => {
      var policy = StackPolicy();
      Apply(sim, policy, 4, 100, 1).Should().BeTrue();
    });
    rollback.InSync.Should().BeTrue();
    rollback.AdvanceAndCompare(60, beforeTick: ApplyAuthoritative).Should().BeEmpty();
  }

  private static void ApplyAuthoritative(SimHarness sim) {
    Apply(sim, StackPolicy(), sim.Frame.Tick % 3 + 1, 20, 1);
    if (sim.Frame.Tick % 2 == 0)
      Apply(sim, StackPolicy(), 1, 20, 2);
  }

  private static BuffApplicationPolicy StackPolicy() => new() {
    Identity = BuffIdentity.SkillPerCaster,
    Reapplication = BuffReapplication.Stack,
    MaxStacks = 2,
    Overflow = BuffStackOverflow.RefreshOldest
  };

  private static bool Apply(SimHarness harness, BuffApplicationPolicy policy, int rank, int duration,
    int casterPlayerId = 1) {
    var frame = harness.Frame;
    var caster = UnitLookup.GetUnitId(ref frame, harness.FindHero(casterPlayerId));
    return BuffsController.Apply(ref frame, harness.FindHero(1), SkillId,
      [new(StatType.Armor, BuffMode.Flat, FP64.One, FP64.One)], rank, duration, caster, policy);
  }

  private readonly record struct Entry(int Caster, int Rank, int Start, int Expiry);

  private static List<Entry> Entries(SimHarness harness) {
    var frame = harness.Frame;
    var target = harness.FindHero(1);
    var entries = new List<Entry>();
    if (!frame.Has<Buffs>(target))
      return entries;
    ref readonly var buffs = ref frame.GetReadOnly<Buffs>(target);
    for (var i = 0; i < Buffs.MaxEntries; i++)
      if (buffs.IsActive(i))
        entries.Add(new Entry(buffs.GetSourceUnitId(i), buffs.GetRank(i),
          buffs.GetStartedTick(i), buffs.GetExpiryTick(i)));
    return entries;
  }

  private static void AdvanceTo(SimHarness harness, int tick) {
    while (harness.Frame.Tick < tick)
      harness.Tick();
  }
}
