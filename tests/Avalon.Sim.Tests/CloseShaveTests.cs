using System.Collections.Generic;
using FluentAssertions;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Deterministic.Navigation;
using xpTURN.Klotho.ECS;
using Xunit;

namespace Meesles.Avalon.Sim.Tests;

// Hairy Wizard's Tertiary: the same timed-buff lifecycle HardenTests owns, on a stat the navigation
// stack reads every tick. What is checked here is the part that is this skill's own - the rank ramp,
// and that a hero already moving picks the new speed up rather than keeping the one it started with.
public class CloseShaveTests {
  private const int CasterPlayerId = 1;
  private const int Tertiary = (int)SkillSlot.Tertiary;

  [Fact]
  public void Cast_RaisesMoveSpeedByTheRowsPercentageOfItsCurrentValue() {
    var harness = SimHarness.CreateInitialized();
    var skill = CloseShaveAsset(harness);
    var speedBefore = MoveSpeed(harness);

    LearnAndCast(harness);

    Stat(harness, StatType.MoveSpeed)
      .Should().Be(speedBefore + speedBefore * skill.BuffPercentAtRank(1));
  }

  // 5% per skill level: rank 1 is 5%, and every rank after is another 5% of the unbuffed speed.
  [Fact]
  public void EachRank_IsWorthAnotherStepOfTheRowsPercentage() {
    var harness = SimHarness.CreateInitialized();
    var skill = CloseShaveAsset(harness);
    skill.BuffDurationMs.Should().BePositive();
    skill.BuffPercentAtRank(1).Should().Be(skill.BuffPercent);
    skill.BuffPercentAtRank(4).Should().Be(skill.BuffPercent + skill.BuffPercentPerRank * FP64.FromInt(3));

    var speedBefore = MoveSpeed(harness);
    LearnAndCast(harness, rank: 3);

    var entry = Entries(harness).Should().ContainSingle().Subject;
    entry.SourceId.Should().Be(AssetIds.SkillHairyWizardTertiary);
    entry.Stat.Should().Be(StatType.MoveSpeed);
    entry.Applied.Should().Be(speedBefore * skill.BuffPercentAtRank(3));
  }

  [Fact]
  public void TheBuffHoldsForTheAuthoredDurationAndThenRevertsExactly() {
    var harness = SimHarness.CreateInitialized();
    var skill = CloseShaveAsset(harness);
    var speedBefore = MoveSpeed(harness);

    var castTick = LearnAndCast(harness);
    var frame = harness.Frame;
    var durationTicks = TickMath.MsToTicksCeil(ref frame, skill.BuffDurationMs);

    AdvanceTo(harness, castTick + durationTicks - 1);
    Stat(harness, StatType.MoveSpeed).Should().BeGreaterThan(speedBefore);

    AdvanceTo(harness, castTick + durationTicks);
    Stat(harness, StatType.MoveSpeed).Should().Be(speedBefore);
    Entries(harness).Should().BeEmpty();
  }

  // The stat is only half of it: NavigationAgentSystem pushes MoveSpeed onto the agent each tick, so a
  // hero already running under a move order speeds up mid-path instead of at the next order.
  [Fact]
  public void AHeroAlreadyMoving_SpeedsUpWithoutANewOrder() {
    var harness = SimHarness.CreateInitialized();
    var hero = harness.FindHero(CasterPlayerId);
    var origin = harness.Frame.GetReadOnly<TransformComponent>(hero).Position;

    harness.Tick(SimHarness.MoveCommand(CasterPlayerId, 0, origin.x, origin.z + FP64.FromInt(12)));
    harness.Tick();
    var agentSpeedBefore = harness.Frame.GetReadOnly<NavAgentComponent>(hero).Speed;

    LearnAndCast(harness);
    harness.Tick(); // NavigationAgentSystem syncs the agent

    harness.Frame.GetReadOnly<NavAgentComponent>(hero).Speed
      .Should().Be(Stat(harness, StatType.MoveSpeed)).And.BeGreaterThan(agentSpeedBefore);
  }

  // --- helpers ---

  // Spends `rank` points into the slot, then casts. Returns the tick the cast executed on, which is
  // what the expiry is measured from.
  private static int LearnAndCast(SimHarness harness, int rank = 1) {
    var frame = harness.Frame;
    var hero = harness.FindHero(CasterPlayerId);
    frame.Get<SkillsComponent>(hero).SkillPoints += rank; // A level-1 hero only carries one
    for (var i = 0; i < rank; i++)
      frame.Get<SkillsComponent>(hero).TrySpendPoint(Tertiary, 4).Should().BeTrue();

    var castTick = harness.Frame.Tick;
    harness.Tick(SimHarness.CastSkillCommand(CasterPlayerId, 0, Tertiary));
    return castTick;
  }

  private static void AdvanceTo(SimHarness harness, int tick) {
    while (harness.Frame.Tick <= tick)
      harness.Tick();
  }

  private static SkillAsset CloseShaveAsset(SimHarness harness) {
    return harness.AssetRegistry.Get<SkillAsset>(AssetIds.SkillHairyWizardTertiary);
  }

  private static FP64 Stat(SimHarness harness, StatType stat) {
    return harness.Frame.GetReadOnly<StatsComponent>(harness.FindHero(CasterPlayerId)).Get(stat);
  }

  private static FP64 MoveSpeed(SimHarness harness) {
    return Stat(harness, StatType.MoveSpeed);
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
