using System.Linq;
using FluentAssertions;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Deterministic.Math;
using Xunit;

namespace Meesles.Avalon.Sim.Tests;

public class BuffsTests {
  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public void InsufficientCapacity_RejectsTheEntireBuff_AndPreservesAnyPreviousApplication(bool refreshing) {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var hero = harness.FindHero(1);
    var desperation = harness.AssetRegistry.Get<SkillAsset>(AssetIds.SkillSkinwalkerUltimate);
    var sprint = harness.AssetRegistry.Get<SkillAsset>(AssetIds.SkillSkinwalkerPrimary);
    BuffsController.ApplySkill(ref frame, hero, desperation, 1).Should().BeTrue();
    if (refreshing)
      BuffsController.Apply(ref frame, hero, sprint.AssetId,
        [new(StatType.MoveSpeed, BuffMode.Percent, FP64.Half, FP64.Zero)], 1, 20).Should().BeTrue();

    harness.Tick();
    frame = harness.Frame;
    var before = harness.StateHash;
    BuffsController.ApplySkill(ref frame, hero, sprint, 1).Should().BeFalse();
    harness.StateHash.Should().Be(before);
    BuffTestQueries.StatChanges(frame, hero).Should().HaveCount(refreshing ? 6 : 5);
  }

  [Fact]
  public void RefreshAtFullCapacity_ReusesTheExistingBuffAndAllItsChangeSlots() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var hero = harness.FindHero(1);
    var desperation = harness.AssetRegistry.Get<SkillAsset>(AssetIds.SkillSkinwalkerUltimate);
    var closeShave = harness.AssetRegistry.Get<SkillAsset>(AssetIds.SkillHairyWizardTertiary);
    BuffsController.ApplySkill(ref frame, hero, desperation, 1).Should().BeTrue();
    BuffsController.ApplySkill(ref frame, hero, closeShave, 1).Should().BeTrue();
    var index = frame.GetReadOnly<Buffs>(hero).FindSlot(desperation.AssetId);
    harness.Tick();
    frame = harness.Frame;

    BuffsController.ApplySkill(ref frame, hero, desperation, 2).Should().BeTrue();
    ref readonly var buffs = ref frame.GetReadOnly<Buffs>(hero);
    buffs.FindSlot(desperation.AssetId).Should().Be(index);
    buffs.GetStartedTick(index).Should().Be(frame.Tick);
    BuffTestQueries.StatChanges(frame, hero).Should().HaveCount(6);
    BuffTestQueries.StatChanges(frame, hero).Count(e => e.SourceId == desperation.AssetId).Should().Be(5);
  }

  [Theory]
  [InlineData(5, 20)]
  [InlineData(10, 20)]
  [InlineData(15, 15)]
  [InlineData(20, 10)]
  [InlineData(30, 0)]
  [InlineData(35, 0)]
  public void Countdown_UsesStoredLifetime_AndClampsAtBothEnds(int tick, int remaining) {
    var buffs = new Buffs();
    buffs.Set(0, 4242, startedTick: 10, expiryTick: 30);

    buffs.GetDurationTicks(0).Should().Be(20);
    buffs.GetRemainingTicks(0, tick).Should().Be(remaining);
    buffs.GetRemainingFraction(0, tick).Should().Be(FP64.FromInt(remaining) / FP64.FromInt(20));

    buffs.Clear(0);
    buffs.GetDurationTicks(0).Should().Be(0);
    buffs.GetRemainingTicks(0, tick).Should().Be(0);
    buffs.GetRemainingFraction(0, tick).Should().Be(FP64.Zero);
  }

  [Fact]
  public void RankedRefresh_StartsANewFullLifetime_WithoutReinterpretingThePreviousApplication() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var hero = harness.FindHero(1);
    var skill = harness.AssetRegistry.Get<SkillAsset>(AssetIds.SkillSkinwalkerPrimary);
    var originalDuration = TickMath.MsToTicksCeil(ref frame, skill.BuffDurationMsAtRank(1));
    var refreshedDuration = TickMath.MsToTicksCeil(ref frame, skill.BuffDurationMsAtRank(2));
    BuffsController.ApplySkill(ref frame, hero, skill, 1);

    harness.Tick();
    harness.Tick();
    frame = harness.Frame;
    var index = frame.GetReadOnly<BuffChanges>(hero).GetBuffIndex(0);
    ref readonly var buffs = ref frame.GetReadOnly<Buffs>(hero);
    buffs.GetDurationTicks(index).Should().Be(originalDuration);
    buffs.GetRemainingTicks(index, frame.Tick).Should().Be(originalDuration - frame.Tick);
    buffs.GetRemainingFraction(index, frame.Tick).Should().BeLessThan(FP64.One);

    BuffsController.ApplySkill(ref frame, hero, skill, 2);
    index = frame.GetReadOnly<BuffChanges>(hero).GetBuffIndex(0);
    AssertSingleBuff(harness, 2);
    buffs.GetDurationTicks(index).Should().Be(refreshedDuration);
    buffs.GetRemainingTicks(index, frame.Tick).Should().Be(refreshedDuration);
    buffs.GetRemainingFraction(index, frame.Tick).Should().Be(FP64.One);
  }

  [Fact]
  public void Reapplication_ReplacesTheWholeChangeSet_AndRestartsItsLifetime() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var hero = harness.FindHero(1);
    var before = frame.GetReadOnly<Stats>(hero);
    BuffsController.Apply(ref frame, hero, 4242,
      [new(StatType.Armor, BuffMode.Flat, FP64.One, FP64.Zero)], 1, 10).Should().BeTrue();
    harness.Tick();
    harness.Tick();
    frame = harness.Frame;
    BuffsController.Apply(ref frame, hero, 4242,
      [new(StatType.MoveSpeed, BuffMode.Flat, FP64.One, FP64.Zero)], 1, 10).Should().BeTrue();

    AssertSingleBuff(harness, 1);
    ref readonly var changes = ref frame.GetReadOnly<BuffChanges>(hero);
    ref readonly var buffs = ref frame.GetReadOnly<Buffs>(hero);
    var index = changes.GetBuffIndex(0);
    buffs.GetStartedTick(index).Should().Be(frame.Tick);
    buffs.GetDurationTicks(index).Should().Be(10);
    buffs.GetRemainingFraction(index, frame.Tick).Should().Be(FP64.One);
    frame.GetReadOnly<Stats>(hero).Armor.Should().Be(before.Armor);
    frame.GetReadOnly<Stats>(hero).MoveSpeed.Should().Be(before.MoveSpeed + FP64.One);
  }

  [Fact]
  public void Harden_GroupsItsChangesIntoOneBuff_AndRefreshesWithoutStacking() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var hero = harness.FindHero(1);
    var skill = harness.AssetRegistry.Get<SkillAsset>(AssetIds.SkillCrystalGiantSecondary);
    var before = frame.GetReadOnly<Stats>(hero);

    BuffsController.ApplySkill(ref frame, hero, skill, 1);
    var firstExpiry = frame.GetReadOnly<Buffs>(hero).GetExpiryTick(0);
    AssertSingleBuff(harness, 2);
    frame.GetReadOnly<BuffChanges>(hero).GetBuffIndex(0).Should()
      .Be(frame.GetReadOnly<BuffChanges>(hero).GetBuffIndex(1));

    harness.Tick();
    frame = harness.Frame;
    BuffsController.ApplySkill(ref frame, hero, skill, 1);
    AssertSingleBuff(harness, 2);
    var index = frame.GetReadOnly<BuffChanges>(hero).GetBuffIndex(0);
    ref readonly var buffs = ref frame.GetReadOnly<Buffs>(hero);
    buffs.GetSourceSkillAssetId(index).Should().Be(skill.AssetId);
    buffs.GetStartedTick(index).Should().Be(frame.Tick);
    buffs.GetExpiryTick(index).Should().BeGreaterThan(firstExpiry);
    frame.GetReadOnly<Stats>(hero).Armor.Should().Be(before.Armor + before.Armor * skill.BuffPercent);
    frame.GetReadOnly<Stats>(hero).MagicResist.Should()
      .Be(before.MagicResist + before.MagicResist * skill.BuffPercent);
  }

  [Fact]
  public void Desperation_CleanseRemovesOnlyHarmfulChanges_ThenExpiryRemovesTheBuff() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var hero = harness.FindHero(1);
    var before = frame.GetReadOnly<Stats>(hero);
    var skill = harness.AssetRegistry.Get<SkillAsset>(AssetIds.SkillSkinwalkerUltimate);
    skill.BuffDurationMs = 50;

    BuffsController.ApplySkill(ref frame, hero, skill, 1);
    AssertSingleBuff(harness, 5);
    var buffIndex = frame.GetReadOnly<BuffChanges>(hero).GetBuffIndex(0);
    var expiry = frame.GetReadOnly<Buffs>(hero).GetExpiryTick(buffIndex);
    BuffsController.ClearHarmful(ref frame, hero).Should().Be(2);

    AssertSingleBuff(harness, 3);
    frame.GetReadOnly<Buffs>(hero).GetExpiryTick(buffIndex).Should().Be(expiry);
    frame.GetReadOnly<Stats>(hero).Armor.Should().Be(before.Armor);
    frame.GetReadOnly<Stats>(hero).MagicResist.Should().Be(before.MagicResist);
    frame.GetReadOnly<Stats>(hero).AttackDamage.Should().BeGreaterThan(before.AttackDamage);

    while (harness.Frame.Tick <= expiry)
      harness.Tick();
    frame = harness.Frame;
    frame.GetReadOnly<Buffs>(hero).IsActive(buffIndex).Should().BeFalse();
    BuffTestQueries.StatChanges(frame, hero).Should().BeEmpty();
    frame.GetReadOnly<Stats>(hero).AttackDamage.Should().Be(before.AttackDamage);
    frame.GetReadOnly<Stats>(hero).MoveSpeed.Should().Be(before.MoveSpeed);
    frame.GetReadOnly<Stats>(hero).BonusAttackSpeed.Should().Be(before.BonusAttackSpeed);
  }

  [Fact]
  public void DifferentSkills_ExpireIndependently() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var hero = harness.FindHero(1);
    var before = frame.GetReadOnly<Stats>(hero);
    BuffsController.Apply(ref frame, hero, 4242,
      [new(StatType.Armor, BuffMode.Flat, FP64.One, FP64.Zero)], 1, 1);
    BuffsController.Apply(ref frame, hero, 4243,
      [new(StatType.MoveSpeed, BuffMode.Flat, FP64.One, FP64.Zero)], 1, 10);
    var entries = BuffTestQueries.StatChanges(frame, hero);
    entries.Select(e => e.ExpiryTick).Distinct().Should().HaveCount(2);

    harness.Tick();
    harness.Tick();
    frame = harness.Frame;
    frame.GetReadOnly<Stats>(hero).Armor.Should().Be(before.Armor);
    frame.GetReadOnly<Stats>(hero).MoveSpeed.Should().Be(before.MoveSpeed + FP64.One);
    AssertSingleBuff(harness, 1);
    BuffsController.ClearAll(ref frame, hero);
    BuffTestQueries.StatChanges(frame, hero).Should().BeEmpty();
    frame.GetReadOnly<Stats>(hero).MoveSpeed.Should().Be(before.MoveSpeed);
  }

  private static void AssertSingleBuff(SimHarness harness, int changeCount) {
    var frame = harness.Frame;
    var hero = harness.FindHero(1);
    var active = 0;
    for (var i = 0; i < Buffs.MaxEntries; i++)
      if (frame.GetReadOnly<Buffs>(hero).IsActive(i))
        active++;
    active.Should().Be(1);
    BuffTestQueries.StatChanges(frame, hero).Should().HaveCount(changeCount);
  }
}
