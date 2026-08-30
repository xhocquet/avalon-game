using FluentAssertions;
using Meesles.Avalon;
using Meesles.Avalon.Sim.Components;
using Xunit;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Tests;

// Exercises the networked deferred-spawn path: no hero exists until a player's faction pick
// (SelectFactionCommand) lands, at which point the hero spawns carrying the chosen Faction.
public class FactionSpawnTests {
  private const int FactionA = 200;
  private const int FactionB = 201;

  [Fact]
  public void DeferredSpawn_NoHeroesUntilFactionSelected() {
    var harness = SimHarness.CreateInitialized(spawnHeroesNow: false);

    harness.Count<Hero>().Should().Be(0);
    harness.Count<PlayerFaction>().Should().Be(SimHarness.DefaultMaxPlayers);

    harness.Tick();
    harness.Count<Hero>().Should().Be(0, "no faction has been selected yet");
  }

  [Fact]
  public void SelectFactionCommand_SpawnsHeroWithChosenFaction() {
    var harness = SimHarness.CreateInitialized(spawnHeroesNow: false);

    harness.Tick(SimHarness.SelectFactionCommand(playerId: 1, tick: 0, factionId: FactionB));

    harness.Count<Hero>().Should().Be(1, "only player 1 has picked");
    EntityRef hero = harness.FindHero(playerId: 1);
    harness.Frame.GetReadOnly<Faction>(hero).FactionId.Should().Be(FactionB);
  }

  [Fact]
  public void UnselectedPlayer_SpawnsWithDefaultFactionAfterGrace() {
    var harness = SimHarness.CreateInitialized(spawnHeroesNow: false);

    // Only player 1 picks; player 2 never does and should fall back to the default after grace.
    harness.Tick(SimHarness.SelectFactionCommand(playerId: 1, tick: 0, factionId: FactionA));
    for (int i = 0; i < 40; i++)
      harness.Tick();

    harness.Count<Hero>().Should().Be(SimHarness.DefaultMaxPlayers);
    harness.Frame.GetReadOnly<Faction>(harness.FindHero(playerId: 1)).FactionId.Should().Be(FactionA);
    harness.Frame.GetReadOnly<Faction>(harness.FindHero(playerId: 2)).FactionId
      .Should().Be(SimulationSetup.DefaultFactionId);
  }
}
