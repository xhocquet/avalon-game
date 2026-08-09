using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using Xunit;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Tests;

// The unit tests drive ScoreSystem directly; this drives the registered pipeline, so the ordering the
// win depends on (TeamPruneSystem settling contenders before ScoreSystem judges) is what is under test.
public class MatchEndIntegrationTests {
  [Fact]
  public void Tick_DestroyingTheLastEnemyCrystalEndsTheMatchForTheSurvivingTeam() {
    var harness = SimHarness.CreateInitialized();
    harness.Tick(); // TeamPruneSystem settles the contender set

    var frame = harness.Frame;
    frame.GetReadOnlySingleton<MatchSetupState>().ContenderTeamCount.Should().Be(2);
    frame.TryGetSingleton<MatchOutcome>(out _).Should().BeFalse();

    frame.Get<Health>(GetCrystalForTeam(ref frame, teamId: 1)).Current = 0;
    harness.Tick();

    frame = harness.Frame;
    ref readonly var outcome = ref frame.GetReadOnlySingleton<MatchOutcome>();
    outcome.WinnerTeamId.Should().Be(2);
    outcome.Reason.Should().Be((int)MatchEndReason.Crystal);

    MatchResultReader.TryRead(ref frame, out var result).Should().BeTrue();
    result.EndTick.Should().Be(outcome.EndTick);
    result.DurationMs.Should().Be(outcome.EndTick * frame.DeltaTimeMs);
    result.Players.Should().HaveCount(2);
  }

  [Fact]
  public void Tick_LeavesTheMatchRunningWhileBothCrystalsStand() {
    var harness = SimHarness.CreateInitialized();

    for (var i = 0; i < 20; i++)
      harness.Tick();

    var frame = harness.Frame;
    frame.TryGetSingleton<MatchOutcome>(out _).Should().BeFalse();
    frame.GetReadOnlySingleton<MatchEndStateComponent>().Ended.Should().BeFalse();
    MatchResultReader.TryRead(ref frame, out _).Should().BeFalse();
  }

  [Fact]
  public void MatchResult_SerializesTheShapeMatchResultSaveSystemWrites() {
    var harness = SimHarness.CreateInitialized();
    harness.Tick();

    var frame = harness.Frame;
    frame.Get<Player>(harness.FindHero(playerId: 2)).HeroKills = 3;
    frame.Get<Health>(GetCrystalForTeam(ref frame, teamId: 1)).Current = 0;
    harness.Tick();

    frame = harness.Frame;
    MatchResultReader.TryRead(ref frame, out var result, playerId => $"Player{playerId}")
      .Should().BeTrue();

    var options = new JsonSerializerOptions { WriteIndented = true };
    options.Converters.Add(new JsonStringEnumConverter());
    var json = JsonSerializer.Serialize(result, options);

    json.Should().Contain("\"WinnerTeamId\": 2");
    json.Should().Contain("\"Reason\": \"Crystal\"");
    json.Should().Contain("\"HeroKills\": 3");
    json.Should().Contain("\"IsWinner\": true");
    json.Should().Contain("\"Name\": \"Player2\"");
    json.Should().Contain("\"HeroAssetId\":");
    json.Should().Contain("\"TickIntervalMs\":");
  }

  [Fact]
  public void TryRead_TalliesEachPlayersResourcesAgainstTheTypesTheMatchUsed() {
    var harness = SimHarness.CreateInitialized();
    harness.Tick();

    var frame = harness.Frame;
    frame.Get<ResourcesComponent>(harness.FindHero(playerId: 2)).Add(AssetIds.PickupTypeWater, 7);
    frame.Get<Health>(GetCrystalForTeam(ref frame, teamId: 1)).Current = 0;
    harness.Tick();

    frame = harness.Frame;
    MatchResultReader.TryRead(ref frame, out var result).Should().BeTrue();

    result.Context.ResourceTypes.Should().ContainSingle()
      .Which.TypeAssetId.Should().Be(AssetIds.PickupTypeWater);

    var winner = result.Players.Single(p => p.PlayerId == 2);
    winner.TotalResources.Should().Be(7);
    winner.Resources.Should().ContainSingle()
      .Which.Should().BeEquivalentTo(new { TypeAssetId = AssetIds.PickupTypeWater, Count = 7 });

    result.Players.Single(p => p.PlayerId == 1).TotalResources.Should().Be(0);
  }

  private static EntityRef GetCrystalForTeam(ref Frame frame, int teamId) {
    var filter = frame.Filter<Crystal, TeamComponent, Health>();
    while (filter.Next(out var entity))
      if (frame.GetReadOnly<TeamComponent>(entity).TeamId == teamId)
        return entity;

    throw new Xunit.Sdk.XunitException($"Expected crystal for team {teamId}.");
  }
}
