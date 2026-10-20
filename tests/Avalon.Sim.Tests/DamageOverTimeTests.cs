using FluentAssertions;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using Xunit;

namespace Meesles.Avalon.Sim.Tests;

public class DamageOverTimeTests {
  private const int SkillA = AssetIds.SkillHairyWizardSecondary;
  private const int SkillB = AssetIds.SkillHairyWizardUltimate;

  [Fact]
  public void DifferentSkills_KeepIndependentDamageClocksAttributionAndExpiry() {
    var harness = SimHarness.CreateInitialized();
    var target = SpawnTarget(harness);
    var frame = harness.Frame;
    var firstSource = harness.FindHero(1);
    var secondSource = harness.FindHero(2);
    var healthBefore = frame.GetReadOnly<Health>(target).Current;
    DamageOverTimeController.Apply(ref frame, target, firstSource, SkillA, FP64.FromInt(40), 100)
      .Should().BeTrue();
    var first = Find(harness, target, SkillA);

    AdvanceTo(harness, 10);
    frame = harness.Frame;
    DamageOverTimeController.Apply(ref frame, target, secondSource, SkillB, FP64.FromInt(80), 200)
      .Should().BeTrue();
    var second = Find(harness, target, SkillB);
    harness.Count<DamageOverTime>().Should().Be(2);
    first.SourceUnitId.Should().Be(UnitLookup.GetUnitId(ref frame, firstSource));
    second.SourceUnitId.Should().Be(UnitLookup.GetUnitId(ref frame, secondSource));

    AdvanceTo(harness, first.NextPayoutTick + 1);
    frame = harness.Frame;
    var firstPayout = FP64.Floor(first.AccrualPerTick * FP64.FromInt(first.NextPayoutTick + 1));
    frame.GetReadOnly<Health>(target).Current.Should().Be(healthBefore - firstPayout);
    frame.GetReadOnly<Health>(target).LastDamagerUnitId.Should().Be(first.SourceUnitId);
    Find(harness, target, SkillB).NextPayoutTick.Should().Be(second.NextPayoutTick);

    AdvanceTo(harness, second.NextPayoutTick + 1);
    frame = harness.Frame;
    var secondPayout = FP64.Floor(second.AccrualPerTick *
      FP64.FromInt(second.NextPayoutTick - second.StartedTick + 1));
    frame.GetReadOnly<Health>(target).Current.Should().Be(healthBefore - firstPayout - secondPayout);
    frame.GetReadOnly<Health>(target).LastDamagerUnitId.Should().Be(second.SourceUnitId);

    AdvanceTo(harness, first.ExpiryTick + 1);
    harness.Count<DamageOverTime>().Should().Be(1);
    Find(harness, target, SkillB).ExpiryTick.Should().Be(second.ExpiryTick);
    AdvanceTo(harness, second.ExpiryTick + 1);
    harness.Count<DamageOverTime>().Should().Be(0);
  }

  [Fact]
  public void SameSkill_RefreshPreservesAccruedDamageAndPayout_AndNewRateAppliesGoingForward() {
    var harness = SimHarness.CreateInitialized();
    var target = SpawnTarget(harness);
    var frame = harness.Frame;
    var source = harness.FindHero(1);
    var healthBefore = frame.GetReadOnly<Health>(target).Current;
    DamageOverTimeController.Apply(ref frame, target, source, SkillA, FP64.FromInt(40), 100);
    AdvanceTo(harness, 10);
    var before = Find(harness, target, SkillA);
    before.Pending.Should().BeGreaterThan(FP64.Zero);

    frame = harness.Frame;
    var refreshedAt = frame.Tick;
    var latestSource = harness.FindHero(2);
    DamageOverTimeController.Apply(ref frame, target, latestSource, SkillA, FP64.FromInt(80), 200);
    var refreshed = Find(harness, target, SkillA);
    refreshed.Pending.Should().Be(before.Pending);
    refreshed.NextPayoutTick.Should().Be(before.NextPayoutTick);
    refreshed.IntervalTicks.Should().Be(before.IntervalTicks);
    refreshed.StartedTick.Should().Be(refreshedAt);
    refreshed.ExpiryTick.Should().Be(refreshedAt + 200);
    refreshed.SourceUnitId.Should().Be(UnitLookup.GetUnitId(ref frame, latestSource));
    refreshed.AccrualPerTick.Should().Be(FP64.FromInt(80) *
      FP64.FromInt(TickMath.DeltaTimeMs(ref frame)) / FP64.FromInt(1000));

    while (harness.Frame.Tick <= before.NextPayoutTick) {
      frame = harness.Frame;
      DamageOverTimeController.Apply(ref frame, target, latestSource, SkillA, FP64.FromInt(80), 200);
      harness.Tick();
    }
    frame = harness.Frame;
    var expected = FP64.Floor(before.Pending + refreshed.AccrualPerTick *
      FP64.FromInt(before.NextPayoutTick - refreshedAt + 1));
    frame.GetReadOnly<Health>(target).Current.Should().Be(healthBefore - expected);
    harness.Count<DamageOverTime>().Should().Be(1);
    Find(harness, target, SkillA).NextPayoutTick.Should().Be(before.NextPayoutTick + before.IntervalTicks);
  }

  [Fact]
  public void SameSkill_OnDifferentTargets_KeepsSeparateInstances() {
    var harness = SimHarness.CreateInitialized();
    var first = SpawnTarget(harness);
    var second = SpawnTarget(harness);
    var frame = harness.Frame;
    var source = harness.FindHero(1);
    DamageOverTimeController.Apply(ref frame, first, source, SkillA, FP64.FromInt(40), 100);
    DamageOverTimeController.Apply(ref frame, second, source, SkillA, FP64.FromInt(80), 100);
    harness.Count<DamageOverTime>().Should().Be(2);

    DamageOverTimeController.Clear(ref frame, first);
    DamageOverTimeController.IsBurning(ref frame, first).Should().BeFalse();
    DamageOverTimeController.IsBurning(ref frame, second).Should().BeTrue();
    harness.Tick();
    harness.Count<DamageOverTime>().Should().Be(1);
    Find(harness, second, SkillA).AccrualPerTick.Should().BeGreaterThan(FP64.Zero);
  }

  [Fact]
  public void Cleanse_ClearsEveryDotOnTheTarget_WithoutPayingPendingDamage() {
    var harness = SimHarness.CreateInitialized();
    var target = SpawnTarget(harness);
    var frame = harness.Frame;
    var source = harness.FindHero(1);
    var healthBefore = frame.GetReadOnly<Health>(target).Current;
    DamageOverTimeController.Apply(ref frame, target, source, SkillA, FP64.FromInt(40), 100);
    DamageOverTimeController.Apply(ref frame, target, source, SkillB, FP64.FromInt(80), 100);
    AdvanceTo(harness, 10);
    frame = harness.Frame;

    BuffsController.ClearNegative(ref frame, target);
    DamageOverTimeController.IsBurning(ref frame, target).Should().BeFalse();
    harness.Tick();
    harness.Count<DamageOverTime>().Should().Be(0);
    harness.Frame.GetReadOnly<Health>(target).Current.Should().Be(healthBefore);
  }

  [Fact]
  public void DestroyedTarget_ReapsAllItsDotEntities() {
    var harness = SimHarness.CreateInitialized();
    var target = SpawnTarget(harness);
    var frame = harness.Frame;
    var source = harness.FindHero(1);
    DamageOverTimeController.Apply(ref frame, target, source, SkillA, FP64.FromInt(40), 100);
    DamageOverTimeController.Apply(ref frame, target, source, SkillB, FP64.FromInt(80), 100);
    frame.DestroyEntity(target);

    harness.Tick();
    harness.Count<DamageOverTime>().Should().Be(0);
  }

  [Fact]
  public void Rollback_RestoresIndependentDotAccrualRefreshAndEffectEntities() {
    var rollback = RollbackHarness.Create();
    rollback.Advance(15, beforeTick: ApplyAuthoritative);
    var target = rollback.Server.FindHero(2);
    Find(rollback.Server, target, SkillA).Pending.Should().BeGreaterThan(FP64.Zero);
    rollback.Server.Count<DamageOverTime>().Should().Be(2);

    rollback.MispredictAndRollback(10, null, beforeTick: sim => {
      var frame = sim.Frame;
      var source = sim.FindHero(1);
      var victim = sim.FindHero(2);
      DamageOverTimeController.Apply(ref frame, victim, source, SkillA, FP64.FromInt(400), 80);
      DamageOverTimeController.Apply(ref frame, victim, source, AssetIds.SkillSnailheadPrimary,
        FP64.FromInt(800), 80);
    });
    rollback.InSync.Should().BeTrue();
    rollback.Client.Count<DamageOverTime>().Should().Be(2);
    rollback.AdvanceAndCompare(120, beforeTick: ApplyAuthoritative).Should().BeEmpty();
  }

  private static void ApplyAuthoritative(SimHarness harness) {
    var frame = harness.Frame;
    var source = harness.FindHero(1);
    var target = harness.FindHero(2);
    if (frame.Tick % 7 == 0)
      DamageOverTimeController.Apply(ref frame, target, source, SkillA, FP64.FromInt(40), 100);
    if (frame.Tick % 11 == 0)
      DamageOverTimeController.Apply(ref frame, target, source, SkillB, FP64.FromInt(80), 100);
  }

  private static EntityRef SpawnTarget(SimHarness harness) {
    var frame = harness.Frame;
    var target = frame.CreateEntity();
    frame.Add(target, new UnitIdentity { UnitId = UnitLookup.NextUnitId(ref frame) });
    frame.Add(target, new Health(FP64.FromInt(10000)));
    return target;
  }

  private static DamageOverTime Find(SimHarness harness, EntityRef target, int skillId) {
    var frame = harness.Frame;
    var targetUnitId = UnitLookup.GetUnitId(ref frame, target);
    var filter = frame.Filter<DamageOverTime>();
    while (filter.Next(out var entity)) {
      var dot = frame.GetReadOnly<DamageOverTime>(entity);
      if (dot.TargetUnitId == targetUnitId && dot.SourceSkillAssetId == skillId)
        return dot;
    }
    throw new System.InvalidOperationException($"No DOT for target {targetUnitId} skill {skillId}");
  }

  private static void AdvanceTo(SimHarness harness, int tick) {
    while (harness.Frame.Tick < tick)
      harness.Tick();
  }
}
