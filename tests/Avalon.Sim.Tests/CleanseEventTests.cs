using System.Linq;
using FluentAssertions;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using Meesles.Avalon.Sim.Heroes;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.ECS;
using Xunit;

namespace Meesles.Avalon.Sim.Tests;

public class CleanseEventTests {
  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public void Refresh_RaisesOneTargetedEvent_EvenWithoutDebuffs(bool hasDebuff) {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var hero = harness.FindHero(1);
    if (hasDebuff) SilenceController.Apply(ref frame, hero, 519, 1000);
    var collector = new EventCollector();
    collector.BeginTick(10);
    frame.EventRaiser = collector;
    var skill = frame.AssetRegistry.Get<SkillAsset>(AssetIds.SkillPickleKnightTertiary);
    var position = frame.GetReadOnly<TransformComponent>(hero).Position;
    var context = new SkillCastContext(hero, 1, 2, skill, 1, position, position);
    new PickleKnightSkills().OnCast(ref frame, in context);
    var evt = collector.Collected.OfType<UnitCleansedEvent>().Should().ContainSingle().Subject;
    evt.UnitId.Should().Be(UnitLookup.GetUnitId(ref frame, hero));
    evt.Mode.Should().Be(EventMode.Synced);
    SilenceController.IsSilenced(ref frame, hero).Should().BeFalse();
  }

  [Fact]
  public void Molt_RaisesCleanseOnlyWhenTheChannelCompletes() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var hero = harness.FindHero(1);
    frame.Get<Hero>(hero).HeroStatsAssetId = AssetIds.HeroSnailhead;
    var collector = new EventCollector();
    collector.BeginTick(10);
    frame.EventRaiser = collector;
    var skill = frame.AssetRegistry.Get<SkillAsset>(AssetIds.SkillSnailheadUltimate);
    var position = frame.GetReadOnly<TransformComponent>(hero).Position;
    var context = new SkillCastContext(hero, 1, 3, skill, 1, position, position);
    new SnailheadSkills().OnCast(ref frame, in context);
    collector.Collected.OfType<UnitCleansedEvent>().Should().BeEmpty();
    frame.Tick = frame.GetReadOnly<SkillChannel>(hero).CompleteTick;
    ChannelController.Complete(ref frame, hero);
    collector.Collected.OfType<UnitCleansedEvent>().Should().ContainSingle()
      .Which.UnitId.Should().Be(UnitLookup.GetUnitId(ref frame, hero));
    ChannelController.Complete(ref frame, hero);
    collector.Collected.OfType<UnitCleansedEvent>().Should().ContainSingle();
  }
}
