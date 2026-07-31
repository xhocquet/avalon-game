using FluentAssertions;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using Xunit;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Deterministic.Navigation;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Tests;

public class NavAgentInitializationTests {
  [Fact]
  public void InitializeWorld_AddsNavAgentsToHeroes() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;

    var filter = frame.Filter<Hero, TransformComponent, NavAgentComponent>();
    while (filter.Next(out var entity)) {
      ref readonly var hero = ref frame.GetReadOnly<Hero>(entity);
      ref readonly var transform = ref frame.Get<TransformComponent>(entity);
      ref readonly var nav = ref frame.Get<NavAgentComponent>(entity);
      var heroAsset = harness.AssetRegistry.Get<HeroAsset>(hero.HeroAssetId);

      nav.Position.Should().Be(transform.Position);
      nav.Speed.Should().Be(heroAsset.MoveSpeed);
      nav.Radius.Should().Be(heroAsset.Radius);
    }

    filter.Count.Should().Be(2);
  }

  [Fact]
  public void WaveSpawn_AddsNavAgentsToMinions() {
    var harness = SimHarness.CreateInitialized();
    var rules = harness.AssetRegistry.Get<WaveRulesAsset>();
    var stats = harness.AssetRegistry.Get<MinionStatsAsset>();

    for (int tick = 0; tick <= rules.FirstWaveDelayTicks; tick++)
      harness.Tick();

    var frame = harness.Frame;
    int count = 0;
    var filter = frame.Filter<Minion, TransformComponent, NavAgentComponent>();
    while (filter.Next(out var entity)) {
      ref readonly var transform = ref frame.Get<TransformComponent>(entity);
      ref readonly var nav = ref frame.Get<NavAgentComponent>(entity);

      nav.Position.Should().Be(transform.Position);
      nav.Speed.Should().Be(stats.MoveSpeed);
      nav.Radius.Should().Be(stats.Radius);
      count++;
    }

    count.Should().Be(rules.MinionsPerWave * 2);
  }

  // The nav agent caches its speed, so a stat change only means something if navigation re-reads
  // it. This is what makes a speed item or a slow debuff move the unit differently.
  [Fact]
  public void MoveSpeedStatChange_ReachesTheNavAgent() {
    var harness = SimHarness.CreateInitialized();
    var hero = harness.FindHero(playerId: 1);

    var frame = harness.Frame;
    var buffed = frame.GetReadOnly<Stats>(hero).MoveSpeed + FP64.FromInt(3);
    frame.Get<Stats>(hero).MoveSpeed = buffed;
    harness.Tick();

    harness.Frame.GetReadOnly<NavAgentComponent>(hero).Speed.Should().Be(buffed);
  }
}
