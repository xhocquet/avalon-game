using System.Linq;
using FluentAssertions;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Deterministic.Math;
using Xunit;

namespace Meesles.Avalon.Sim.Tests;

public class BuffsTests {
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
  public void SameSource_DifferentLifetimes_ExpireIndependently() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var hero = harness.FindHero(1);
    var before = frame.GetReadOnly<Stats>(hero);
    BuffsController.Apply(ref frame, hero, 4242, StatType.Armor, FP64.One, FP64.Zero, 1);
    BuffsController.Apply(ref frame, hero, 4242, StatType.MoveSpeed, FP64.One, FP64.Zero, 10);
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
