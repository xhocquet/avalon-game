using FluentAssertions;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using Xunit;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Tests;

// A hero's numbers come from the HeroAsset its faction names, not from the global player/minion
// stat rows. These assertions are what stops the two drifting back together.
public class HeroAssetSpawnTests {
  private const int FactionA = 200;
  private const int FactionB = 201;

  [Fact]
  public void SpawnedHero_CarriesTheHeroAssetItsFactionNames() {
    var harness = SimHarness.CreateInitialized(spawnHeroesNow: false);

    harness.Tick(SimHarness.SelectFactionCommand(playerId: 1, tick: 0, factionId: FactionB));

    var expected = harness.AssetRegistry.Get<FactionAsset>(FactionB).HeroAssetId;
    harness.Frame.GetReadOnly<Hero>(harness.FindHero(playerId: 1)).HeroAssetId.Should().Be(expected);
  }

  [Fact]
  public void SpawnedHero_TakesItsStatsFromItsHeroRow() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;

    var filter = frame.Filter<Hero>();
    filter.Count.Should().Be(SimHarness.DefaultMaxPlayers);

    while (filter.Next(out var entity)) {
      var heroAssetId = frame.GetReadOnly<Hero>(entity).HeroAssetId;
      var heroAsset = harness.AssetRegistry.Get<HeroAsset>(heroAssetId);

      frame.GetReadOnly<StatsComponent>(entity).MaxHealth.Should().Be(heroAsset.Health);
      frame.GetReadOnly<StatsComponent>(entity).Strength.Should().Be(heroAsset.AttackDamage);
      frame.GetReadOnly<StatsComponent>(entity).MoveSpeed.Should().Be(heroAsset.MoveSpeed);

      ref readonly var combat = ref frame.GetReadOnly<Combat>(entity);
      combat.AttackRange.Should().Be(heroAsset.AttackRange);
      combat.AttackCooldownTicks.Should().Be(heroAsset.AttackCooldownTicks);
    }
  }

  // Two players picking different factions get heroes built from different rows. Asserting each
  // hero against its own faction's row (rather than against each other) keeps this honest whether
  // or not the two rows are currently tuned apart.
  [Fact]
  public void HeroesOfDifferentFactions_EachTakeTheirOwnRow() {
    var harness = SimHarness.CreateInitialized(spawnHeroesNow: false);

    harness.Tick(SimHarness.SelectFactionCommand(playerId: 1, tick: 0, factionId: FactionA));
    harness.Tick(SimHarness.SelectFactionCommand(playerId: 2, tick: 1, factionId: FactionB));

    AssertHeroMatchesFaction(harness, playerId: 1, factionId: FactionA);
    AssertHeroMatchesFaction(harness, playerId: 2, factionId: FactionB);
  }

  private static void AssertHeroMatchesFaction(SimHarness harness, int playerId, int factionId) {
    var expected = harness.AssetRegistry.Get<HeroAsset>(
      harness.AssetRegistry.Get<FactionAsset>(factionId).HeroAssetId);

    var frame = harness.Frame;
    var hero = harness.FindHero(playerId);

    frame.GetReadOnly<StatsComponent>(hero).MaxHealth.Should().Be(expected.Health);
    frame.GetReadOnly<StatsComponent>(hero).Strength.Should().Be(expected.AttackDamage);
    frame.GetReadOnly<StatsComponent>(hero).MoveSpeed.Should().Be(expected.MoveSpeed);
    frame.GetReadOnly<Combat>(hero).AttackRange.Should().Be(expected.AttackRange);
    frame.GetReadOnly<Combat>(hero).AttackCooldownTicks.Should().Be(expected.AttackCooldownTicks);
  }
}
