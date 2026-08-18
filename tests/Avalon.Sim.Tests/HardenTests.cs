using System.Collections.Generic;
using FluentAssertions;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using Xunit;

namespace Meesles.Avalon.Sim.Tests;

// Crystal Giant's Secondary, and with it the timed-buff lifecycle: a modifier that goes on at cast,
// holds for the row's duration, and comes back off at a tick fixed when it landed.
public class HardenTests {
  private const int CasterPlayerId = 1;
  private const int Secondary = (int)SkillSlot.Secondary;

  [Fact]
  public void Cast_RaisesBothResistsByTheRowsPercentageOfTheirCurrentValue() {
    var harness = CreateCrystalGiantHarness();
    var skill = HardenAsset(harness);
    var armorBefore = Stat(harness, StatType.Armor);
    var resistBefore = Stat(harness, StatType.MagicResist);

    LearnAndCast(harness);

    var percent = skill.BuffPercentAtRank(1);
    Stat(harness, StatType.Armor).Should().Be(armorBefore + armorBefore * percent);
    Stat(harness, StatType.MagicResist).Should().Be(resistBefore + resistBefore * percent);
  }

  // The whole buff is authored in the one row: duration, the rank-1 percentage, and the step per rank.
  // A hardcoded 5% or 10s fails here rather than quietly shipping.
  [Fact]
  public void EveryBuffNumber_TracesBackToTheAssetRow() {
    var harness = CreateCrystalGiantHarness();
    var skill = HardenAsset(harness);

    skill.BuffDurationMs.Should().BePositive();
    skill.BuffPercent.Should().BeGreaterThan(FP64.Zero);
    skill.BuffPercentAtRank(2).Should().Be(skill.BuffPercent + skill.BuffPercentPerRank);

    var armorBefore = Stat(harness, StatType.Armor);
    LearnAndCast(harness);

    var entry = Entries(harness).Should().ContainSingle(e => e.Stat == StatType.Armor).Subject;
    entry.SourceId.Should().Be(AssetIds.SkillCrystalGiantSecondary);
    entry.Applied.Should().Be(armorBefore * skill.BuffPercentAtRank(1));
  }

  [Fact]
  public void TheBuffHoldsForTheAuthoredDurationAndThenRevertsExactly() {
    var harness = CreateCrystalGiantHarness();
    var skill = HardenAsset(harness);
    var armorBefore = Stat(harness, StatType.Armor);
    var resistBefore = Stat(harness, StatType.MagicResist);

    var castTick = LearnAndCast(harness);
    var durationTicks = DurationTicks(harness, skill);

    // Held right up to the last tick of the duration.
    AdvanceTo(harness, castTick + durationTicks - 1);
    Stat(harness, StatType.Armor).Should().BeGreaterThan(armorBefore);

    AdvanceTo(harness, castTick + durationTicks);
    Stat(harness, StatType.Armor).Should().Be(armorBefore);
    Stat(harness, StatType.MagicResist).Should().Be(resistBefore);
    Entries(harness).Should().BeEmpty();
  }

  // A percentage of a stat that moved under the buff would refund the wrong amount if the revert
  // recomputed it, so the entry keeps the delta it actually applied.
  [Fact]
  public void ALevelUpMidBuff_DoesNotChangeWhatTheBuffGivesBackOnExpiry() {
    var harness = CreateCrystalGiantHarness();
    var skill = HardenAsset(harness);
    var hero = harness.FindHero(CasterPlayerId);

    var castTick = LearnAndCast(harness);
    var applied = Entries(harness).Should().ContainSingle(e => e.Stat == StatType.Armor).Subject.Applied;

    var frame = harness.Frame;
    frame.Get<ExperienceComponent>(hero).Experience =
      harness.AssetRegistry.Get<XpRulesAsset>().TotalXpForLevel(3);
    harness.Tick(); // ExperienceSystem grants the level and its armor growth

    var armorWhileBuffed = Stat(harness, StatType.Armor);
    AdvanceTo(harness, castTick + DurationTicks(harness, skill));

    Stat(harness, StatType.Armor).Should().Be(armorWhileBuffed - applied);
  }

  [Fact]
  public void Recasting_RefreshesTheBuffRatherThanStackingASecondCopy() {
    var harness = CreateCrystalGiantHarness();
    var skill = HardenAsset(harness);
    var armorBefore = Stat(harness, StatType.Armor);

    var firstCastTick = LearnAndCast(harness);
    var armorBuffed = Stat(harness, StatType.Armor);

    AdvanceTo(harness, firstCastTick + CooldownTicks(harness, skill));
    harness.Tick(SimHarness.CastSkillCommand(CasterPlayerId, 0, Secondary));
    var recastTick = harness.Frame.Tick - 1;

    // One entry per stat, and the stat sits where a single copy puts it - not compounded off itself.
    Entries(harness).Should().HaveCount(2);
    Stat(harness, StatType.Armor).Should().Be(armorBuffed);

    // The clock restarted: still up where the first cast's would have expired.
    AdvanceTo(harness, firstCastTick + DurationTicks(harness, skill));
    Stat(harness, StatType.Armor).Should().Be(armorBuffed);

    AdvanceTo(harness, recastTick + DurationTicks(harness, skill));
    Stat(harness, StatType.Armor).Should().Be(armorBefore);
  }

  [Fact]
  public void Death_TakesTheBuffOffRatherThanLettingItRunOutOnACorpse() {
    var harness = CreateCrystalGiantHarness();
    var armorBefore = Stat(harness, StatType.Armor);

    LearnAndCast(harness);
    Stat(harness, StatType.Armor).Should().BeGreaterThan(armorBefore);

    harness.Frame.Get<Health>(harness.FindHero(CasterPlayerId)).Current = FP64.Zero;
    harness.Tick(); // RespawnSystem picks the death up

    Stat(harness, StatType.Armor).Should().Be(armorBefore);
    Entries(harness).Should().BeEmpty();
  }

  // --- setup helpers ---

  // The harness defaults every player to Hairy Wizards, whose skill set is empty, so go through the
  // real faction-select path to get a Crystal Giant on the board.
  private static SimHarness CreateCrystalGiantHarness() {
    var harness = SimHarness.CreateInitialized(spawnHeroesNow: false);
    harness.Tick(
      SimHarness.SelectFactionCommand(1, 0, AssetIds.FactionCrystalWarriors),
      SimHarness.SelectFactionCommand(2, 0, AssetIds.FactionCrystalWarriors));

    return harness;
  }

  // Returns the tick the cast executed on, which is what the expiry is measured from.
  private static int LearnAndCast(SimHarness harness) {
    harness.Tick(SimHarness.UpgradeSkillCommand(CasterPlayerId, 0, Secondary));

    var castTick = harness.Frame.Tick;
    harness.Tick(SimHarness.CastSkillCommand(CasterPlayerId, 0, Secondary));
    return castTick;
  }

  private static void AdvanceTo(SimHarness harness, int tick) {
    while (harness.Frame.Tick <= tick)
      harness.Tick();
  }

  private static SkillAsset HardenAsset(SimHarness harness) {
    return harness.AssetRegistry.Get<SkillAsset>(AssetIds.SkillCrystalGiantSecondary);
  }

  private static int DurationTicks(SimHarness harness, SkillAsset skill) {
    var frame = harness.Frame;
    return TickMath.MsToTicksCeil(ref frame, skill.BuffDurationMs);
  }

  private static int CooldownTicks(SimHarness harness, SkillAsset skill) {
    var frame = harness.Frame;
    return TickMath.MsToTicksCeil(ref frame, skill.CooldownMs);
  }

  private static FP64 Stat(SimHarness harness, StatType stat) {
    return harness.Frame.GetReadOnly<StatsComponent>(harness.FindHero(CasterPlayerId)).Get(stat);
  }

  private readonly record struct BuffEntry(int SourceId, StatType Stat, FP64 Applied, int ExpiryTick);

  private static List<BuffEntry> Entries(SimHarness harness) {
    var entries = new List<BuffEntry>();
    var hero = harness.FindHero(CasterPlayerId);
    if (!harness.Frame.Has<StatBuffsComponent>(hero))
      return entries;

    ref readonly var buffs = ref harness.Frame.GetReadOnly<StatBuffsComponent>(hero);
    for (var i = 0; i < StatBuffsComponent.MaxEntries; i++)
      if (buffs.IsActive(i))
        entries.Add(new BuffEntry(buffs.GetSourceId(i), buffs.GetStat(i), buffs.GetApplied(i),
          buffs.GetExpiryTick(i)));

    return entries;
  }
}
