using FluentAssertions;
using Meesles.Avalon;
using Xunit;

namespace Meesles.Avalon.Sim.Tests;

public class SimHarnessTests {
  [Fact]
  public void CreateInitialized_LoadsAssetsAndCreatesInitialWorld() {
    var harness = SimHarness.CreateInitialized();

    harness.AssetRegistry.Get<PlayerStatsAsset>().Should().NotBeNull();
    harness.AssetRegistry.Get<WaveRulesAsset>().Should().NotBeNull();

    harness.Count<Hero>().Should().Be(2);
    harness.Count<Base>().Should().Be(2);
    harness.Count<SpawnPoint>().Should().Be(2);
    harness.Count<UnitIdState>().Should().Be(1);
    harness.Frame.Tick.Should().Be(0);
  }
}
