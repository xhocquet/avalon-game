using FluentAssertions;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Models;
using Xunit;
using xpTURN.Klotho.Deterministic.Navigation;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Tests;

public class NavAgentInitializationTests {
  [Fact]
  public void InitializeWorld_AddsNavAgentsToHeroes() {
    var harness = SimHarness.CreateInitialized();
    var stats = harness.AssetRegistry.Get<PlayerStatsAsset>();
    var frame = harness.Frame;

    int count = 0;
    var filter = frame.Filter<Hero, TransformComponent, NavAgentComponent>();
    while (filter.Next(out var entity)) {
      ref readonly var transform = ref frame.Get<TransformComponent>(entity);
      ref readonly var nav = ref frame.Get<NavAgentComponent>(entity);

      nav.Position.Should().Be(transform.Position);
      nav.Speed.Should().Be(stats.MoveSpeed);
      count++;
    }

    count.Should().Be(2);
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
      count++;
    }

    count.Should().Be(rules.MinionsPerWave * 2);
  }
}
