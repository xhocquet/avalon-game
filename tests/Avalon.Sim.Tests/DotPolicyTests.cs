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

public class DotPolicyTests {
  private const int SkillId = AssetIds.SkillHairyWizardSecondary;

  [Fact]
  public void AuthoredDotPolicy_SurvivesJsonAndBinarySerialization_IndependentlyOfStatBuffPolicy() {
    WarmupRegistry.RunAll();
    var bytes = DataAssetJsonConverter.ConvertMixedJsonToBytes("""
      [{
        "$type": "Meesles.Avalon.Sim.Assets.SkillAsset",
        "AssetId": 501,
        "DotIdentityMode": 1,
        "DotReapply": 1,
        "DotDurationMode": 2,
        "DotMaxStacks": 3,
        "DotOverflow": 2
      }]
      """);
    var skill = (SkillAsset)DataAssetReader.LoadMixedCollectionFromBytes(bytes).Single();
    skill.DotPolicy.Should().BeEquivalentTo(new BuffApplicationPolicy {
      Identity = BuffIdentity.SkillPerCaster,
      Reapplication = BuffReapplication.Stack,
      Duration = BuffDurationPolicy.Extend,
      MaxStacks = 3,
      Overflow = BuffStackOverflow.ReplaceWeakest
    });
    skill.BuffPolicy.Should().BeEquivalentTo(BuffApplicationPolicy.Default);
  }

  [Fact]
  public void DefaultPolicy_OnlyRefreshes_AndPreservesPendingDamageAndPayoutClock() {
    var harness = Create(out var target);
    Skill(harness).DotPolicy.Should().BeEquivalentTo(BuffApplicationPolicy.Default);
    Apply(harness, target).Should().BeTrue();
    AdvanceTo(harness, 10);
    var before = Entries(harness, target).Should().ContainSingle().Subject;
    Skill(harness).DotDamagePerSecond = FP64.FromInt(80);
    Apply(harness, target, 2).Should().BeTrue();

    var after = Entries(harness, target).Should().ContainSingle().Subject;
    after.Effect.Should().Be(before.Effect);
    after.State.Pending.Should().Be(before.State.Pending);
    after.State.NextPayoutTick.Should().Be(before.State.NextPayoutTick);
    after.State.AccrualPerTick.Should().BeGreaterThan(before.State.AccrualPerTick);
    after.State.SourceUnitId.Should().Be(CasterId(harness, 2));
    after.State.StartedTick.Should().Be(harness.Frame.Tick);
  }

  [Fact]
  public void SameSkillStacks_DealIndependentDamage_WithTheirOwnClocksAttributionAndExpiry() {
    var harness = Create(out var target);
    ConfigureStacking(harness, 2);
    Apply(harness, target).Should().BeTrue();
    var first = Entries(harness, target).Single().State;
    AdvanceTo(harness, 10);
    Skill(harness).DotDamagePerSecond = FP64.FromInt(80);
    Apply(harness, target, 2).Should().BeTrue();
    var second = Entries(harness, target).Single(e => e.State.SourceUnitId == CasterId(harness, 2)).State;
    var healthBefore = harness.Frame.GetReadOnly<Health>(target).Current;

    AdvanceTo(harness, first.NextPayoutTick + 1);
    var firstPayout = FP64.Floor(first.AccrualPerTick * FP64.FromInt(first.NextPayoutTick + 1));
    harness.Frame.GetReadOnly<Health>(target).Current.Should().Be(healthBefore - firstPayout);
    harness.Frame.GetReadOnly<Health>(target).LastDamagerUnitId.Should().Be(first.SourceUnitId);

    AdvanceTo(harness, second.NextPayoutTick + 1);
    var secondPayout = FP64.Floor(second.AccrualPerTick *
      FP64.FromInt(second.NextPayoutTick - second.StartedTick + 1));
    harness.Frame.GetReadOnly<Health>(target).Current.Should().Be(healthBefore - firstPayout - secondPayout);
    harness.Frame.GetReadOnly<Health>(target).LastDamagerUnitId.Should().Be(second.SourceUnitId);

    AdvanceTo(harness, first.ExpiryTick + 1);
    Entries(harness, target).Should().ContainSingle(e => e.State.ExpiryTick == second.ExpiryTick);
    AdvanceTo(harness, second.ExpiryTick + 1);
    harness.Count<DamageOverTime>().Should().Be(0);
  }

  [Fact]
  public void RejectAtCap_IsAtomic_AndDoesNotLimitAnotherSkill() {
    var harness = Create(out var target);
    ConfigureStacking(harness, 2);
    Apply(harness, target).Should().BeTrue();
    Apply(harness, target).Should().BeTrue();
    AdvanceTo(harness, 10);
    var hash = harness.StateHash;
    Apply(harness, target, 2).Should().BeFalse();
    harness.StateHash.Should().Be(hash);

    var frame = harness.Frame;
    DamageOverTimeController.Apply(ref frame, target, harness.FindHero(2), AssetIds.SkillHairyWizardUltimate,
      FP64.FromInt(80), 100).Should().BeTrue();
    harness.Count<DamageOverTime>().Should().Be(3);
    Entries(harness, target).Should().HaveCount(2);
  }

  [Fact]
  public void PerCasterIdentity_GivesEachCasterAnIndependentCap() {
    var harness = Create(out var target);
    ConfigureStacking(harness, 2);
    Skill(harness).DotIdentityMode = (int)BuffIdentity.SkillPerCaster;
    foreach (var player in new[] { 1, 2 }) {
      Apply(harness, target, player).Should().BeTrue();
      Apply(harness, target, player).Should().BeTrue();
      Apply(harness, target, player).Should().BeFalse();
    }
    Entries(harness, target).Should().HaveCount(4);
    foreach (var caster in Entries(harness, target).GroupBy(e => e.State.SourceUnitId))
      caster.Should().HaveCount(2);
  }

  [Fact]
  public void StackCapsAndCleanse_AreScopedToTheTarget() {
    var harness = Create(out var target);
    ConfigureStacking(harness, 2);
    var frame = harness.Frame;
    var other = frame.CreateEntity();
    frame.Add(other, new UnitIdentity { UnitId = UnitLookup.NextUnitId(ref frame) });
    frame.Add(other, new Health(FP64.FromInt(10000)));
    foreach (var victim in new[] { target, other }) {
      Apply(harness, victim).Should().BeTrue();
      Apply(harness, victim).Should().BeTrue();
      Apply(harness, victim).Should().BeFalse();
    }
    AdvanceTo(harness, 10);
    frame = harness.Frame;
    BuffsController.ClearNegative(ref frame, target);
    harness.Tick();
    Entries(harness, target).Should().BeEmpty();
    Entries(harness, other).Should().HaveCount(2);
    harness.Count<DamageOverTime>().Should().Be(2);
    harness.Frame.GetReadOnly<Health>(target).Current.Should().Be(FP64.FromInt(10000));
  }

  [Fact]
  public void SkillApplication_RetainsAProjectileCastersId_AfterTheCasterWasRemoved() {
    var harness = Create(out var target);
    var casterId = CasterId(harness, 1);
    Skill(harness).DotIdentityMode = (int)BuffIdentity.SkillPerCaster;
    var frame = harness.Frame;
    frame.DestroyEntity(harness.FindHero(1));

    DamageOverTimeController.ApplySkill(ref frame, target, casterId, Skill(harness), 1).Should().BeTrue();
    Entries(harness, target).Should().ContainSingle(e => e.State.SourceUnitId == casterId);
  }

  [Theory]
  [InlineData(BuffStackOverflow.RefreshOldest)]
  [InlineData(BuffStackOverflow.ReplaceWeakest)]
  public void EqualRateAndAge_TiesUseTheEffectEntityId(BuffStackOverflow overflow) {
    var harness = Create(out var target);
    ConfigureStacking(harness, 2, overflow);
    Apply(harness, target).Should().BeTrue();
    Apply(harness, target, 2).Should().BeTrue();
    var selected = Entries(harness, target).OrderBy(e => e.Effect.ToId()).First();
    AdvanceTo(harness, 10);
    Apply(harness, target, 2).Should().BeTrue();

    var updated = Entries(harness, target, true).Single(e => e.Effect == selected.Effect).State;
    if (overflow == BuffStackOverflow.ReplaceWeakest)
      updated.ExpiryTick.Should().Be(10);
    else
      updated.StartedTick.Should().Be(10);
  }

  [Fact]
  public void Ignore_PreservesTheEntireExistingInstance() {
    var harness = Create(out var target);
    Skill(harness).DotReapply = (int)BuffReapplication.Ignore;
    Apply(harness, target).Should().BeTrue();
    AdvanceTo(harness, 10);
    var hash = harness.StateHash;
    Skill(harness).DotDamagePerSecond = FP64.FromInt(200);
    Apply(harness, target, 2).Should().BeFalse();
    harness.StateHash.Should().Be(hash);
  }

  [Theory]
  [InlineData(BuffDurationPolicy.Restart, 160, 5, 15)]
  [InlineData(BuffDurationPolicy.KeepLaterExpiry, 160, 0, 20)]
  [InlineData(BuffDurationPolicy.KeepLaterExpiry, 240, 0, 20)]
  [InlineData(BuffDurationPolicy.KeepLaterExpiry, 480, 5, 35)]
  [InlineData(BuffDurationPolicy.Extend, 160, 0, 30)]
  public void Refresh_UsesAuthoredTiming_WithoutResettingAccrualOrPayout(BuffDurationPolicy timing,
    int incomingMs, int expectedStart, int expectedExpiry) {
    var harness = Create(out var target);
    Skill(harness).DotDurationMode = (int)timing;
    Skill(harness).DotDurationMs = 320;
    Apply(harness, target).Should().BeTrue();
    AdvanceTo(harness, 5);
    var before = Entries(harness, target).Single().State;
    Skill(harness).DotDurationMs = incomingMs;
    Apply(harness, target).Should().BeTrue();

    var after = Entries(harness, target).Single().State;
    after.StartedTick.Should().Be(expectedStart);
    after.ExpiryTick.Should().Be(expectedExpiry);
    after.Pending.Should().Be(before.Pending);
    after.NextPayoutTick.Should().Be(before.NextPayoutTick);
  }

  [Fact]
  public void RefreshOldestAtCap_PreservesItsPendingDamage_AndLeavesTheOtherStackUntouched() {
    var harness = Create(out var target);
    ConfigureStacking(harness, 2, BuffStackOverflow.RefreshOldest);
    Apply(harness, target).Should().BeTrue();
    AdvanceTo(harness, 5);
    Skill(harness).DotDamagePerSecond = FP64.FromInt(80);
    Apply(harness, target, 2).Should().BeTrue();
    AdvanceTo(harness, 10);
    var before = Entries(harness, target);
    var oldest = before.Single(e => e.State.StartedTick == 0);
    var newer = before.Single(e => e.State.StartedTick == 5);
    var healthBefore = harness.Frame.GetReadOnly<Health>(target).Current;
    Skill(harness).DotDamagePerSecond = FP64.FromInt(120);
    Apply(harness, target, 2).Should().BeTrue();

    var after = Entries(harness, target);
    after.Should().HaveCount(2);
    var refreshed = after.Single(e => e.Effect == oldest.Effect).State;
    refreshed.StartedTick.Should().Be(10);
    refreshed.Pending.Should().Be(oldest.State.Pending);
    refreshed.NextPayoutTick.Should().Be(oldest.State.NextPayoutTick);
    after.Single(e => e.Effect == newer.Effect).Should().Be(newer);
    AdvanceTo(harness, oldest.State.NextPayoutTick + 1);
    var payout = FP64.Floor(oldest.State.Pending + refreshed.AccrualPerTick *
      FP64.FromInt(oldest.State.NextPayoutTick - refreshed.StartedTick + 1));
    harness.Frame.GetReadOnly<Health>(target).Current.Should().Be(healthBefore - payout);
  }

  [Fact]
  public void ReplaceWeakestAtCap_SettlesTheOldStacksDamageAndAttribution_AndStartsANewStack() {
    var harness = Create(out var target);
    ConfigureStacking(harness, 2, BuffStackOverflow.ReplaceWeakest);
    Skill(harness).DotDurationMode = (int)BuffDurationPolicy.Extend;
    Skill(harness).DotDamagePerSecond = FP64.FromInt(80);
    Apply(harness, target).Should().BeTrue();
    AdvanceTo(harness, 5);
    Skill(harness).DotDamagePerSecond = FP64.FromInt(40);
    Apply(harness, target, 2).Should().BeTrue();
    AdvanceTo(harness, 10);
    var before = Entries(harness, target);
    var weakest = before.Single(e => e.State.SourceUnitId == CasterId(harness, 2));
    var healthBefore = harness.Frame.GetReadOnly<Health>(target).Current;
    Skill(harness).DotDamagePerSecond = FP64.FromInt(120);
    Apply(harness, target).Should().BeTrue();

    Entries(harness, target).Should().HaveCount(2);
    var retired = Entries(harness, target, true).Single(e => e.Effect == weakest.Effect).State;
    retired.ExpiryTick.Should().Be(10);
    retired.Pending.Should().Be(weakest.State.Pending);
    var replacement = Entries(harness, target).Single(e => e.State.StartedTick == 10).State;
    replacement.Pending.Should().Be(FP64.Zero);
    replacement.NextPayoutTick.Should().Be(10 + weakest.State.IntervalTicks);
    replacement.ExpiryTick.Should().Be(135);

    harness.Tick();
    harness.Count<DamageOverTime>().Should().Be(2);
    harness.Frame.GetReadOnly<Health>(target).Current.Should().Be(healthBefore - FP64.Floor(weakest.State.Pending));
    harness.Frame.GetReadOnly<Health>(target).LastDamagerUnitId.Should().Be(weakest.State.SourceUnitId);
  }

  [Theory]
  [InlineData(BuffReapplication.Refresh)]
  [InlineData(BuffReapplication.Stack)]
  [InlineData(BuffReapplication.Ignore)]
  public void ExpiredStacks_DoNotBlockReapplication_AndStillPayTheirAccruedDamage(BuffReapplication reapply) {
    var harness = Create(out var target);
    Skill(harness).DotReapply = (int)reapply;
    Skill(harness).DotDurationMs = 32;
    Apply(harness, target).Should().BeTrue();
    AdvanceTo(harness, 2);
    var old = Entries(harness, target, true).Single().State;
    var healthBefore = harness.Frame.GetReadOnly<Health>(target).Current;
    Apply(harness, target, 2).Should().BeTrue();
    Entries(harness, target).Should().ContainSingle(e => e.State.StartedTick == 2);

    harness.Tick();
    harness.Count<DamageOverTime>().Should().Be(1);
    harness.Frame.GetReadOnly<Health>(target).Current.Should().Be(healthBefore - FP64.Floor(old.Pending));
    harness.Frame.GetReadOnly<Health>(target).LastDamagerUnitId.Should().Be(old.SourceUnitId);
  }

  [Theory]
  [InlineData(BuffStackOverflow.Reject)]
  [InlineData(BuffStackOverflow.RefreshOldest)]
  [InlineData(BuffStackOverflow.ReplaceWeakest)]
  public void Rollback_RestoresStackedAccrualCapsAndReplacementEntities(BuffStackOverflow overflow) {
    var rollback = RollbackHarness.Create();
    ConfigureStacking(rollback.Server, 3, overflow);
    ConfigureStacking(rollback.Client, 3, overflow);
    rollback.Advance(15, beforeTick: ApplyAuthoritative);
    Entries(rollback.Server, rollback.Server.FindHero(2)).Should().HaveCount(3);
    rollback.MispredictAndRollback(10, null, beforeTick: sim => {
      var frame = sim.Frame;
      var target = sim.FindHero(2);
      DamageOverTimeController.Clear(ref frame, target);
      DamageOverTimeController.Apply(ref frame, target, sim.FindHero(1), SkillId, FP64.FromInt(400), 100,
        new BuffApplicationPolicy { Reapplication = BuffReapplication.Stack, MaxStacks = 3 }).Should().BeTrue();
    });
    rollback.InSync.Should().BeTrue();
    rollback.AdvanceAndCompare(140, beforeTick: ApplyAuthoritative).Should().BeEmpty();
  }

  private static void ApplyAuthoritative(SimHarness harness) {
    if (harness.Frame.Tick % 4 != 0)
      return;
    Skill(harness).DotDamagePerSecond = FP64.FromInt(40 + harness.Frame.Tick % 3 * 20);
    Apply(harness, harness.FindHero(2));
  }

  private static SimHarness Create(out EntityRef target) {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    target = frame.CreateEntity();
    frame.Add(target, new UnitIdentity { UnitId = UnitLookup.NextUnitId(ref frame) });
    frame.Add(target, new Health(FP64.FromInt(10000)));
    Skill(harness).DotDamagePerSecond = FP64.FromInt(40);
    Skill(harness).DotDamagePerSecondPerRank = FP64.Zero;
    Skill(harness).DotDurationMs = 2000;
    return harness;
  }

  private static void ConfigureStacking(SimHarness harness, int cap,
    BuffStackOverflow overflow = BuffStackOverflow.Reject) {
    Skill(harness).DotReapply = (int)BuffReapplication.Stack;
    Skill(harness).DotMaxStacks = cap;
    Skill(harness).DotOverflow = (int)overflow;
  }

  private static SkillAsset Skill(SimHarness harness) => harness.AssetRegistry.Get<SkillAsset>(SkillId);

  private static int CasterId(SimHarness harness, int player) =>
    harness.Frame.GetReadOnly<UnitIdentity>(harness.FindHero(player)).UnitId;

  private static bool Apply(SimHarness harness, EntityRef target, int player = 1) {
    var frame = harness.Frame;
    return DamageOverTimeController.ApplySkill(ref frame, target, CasterId(harness, player), Skill(harness), 1);
  }

  private readonly record struct Entry(EntityRef Effect, DamageOverTime State);

  private static List<Entry> Entries(SimHarness harness, EntityRef target, bool includeExpired = false) {
    var frame = harness.Frame;
    var targetId = UnitLookup.GetUnitId(ref frame, target);
    var result = new List<Entry>();
    var filter = frame.Filter<DamageOverTime>();
    while (filter.Next(out var effect)) {
      var dot = frame.GetReadOnly<DamageOverTime>(effect);
      if (dot.TargetUnitId == targetId && dot.SourceSkillAssetId == SkillId &&
          (includeExpired || !dot.IsExpired(frame.Tick)))
        result.Add(new Entry(effect, dot));
    }
    return result;
  }

  private static void AdvanceTo(SimHarness harness, int tick) {
    while (harness.Frame.Tick < tick)
      harness.Tick();
  }
}
