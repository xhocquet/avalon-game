using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Meesles.Avalon;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using Xunit;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Tests;

// Exercises the deferred (networked-lobby) path where InitializeWorld seeds a base for every team
// the map authors, and TeamPruneSystem removes the bases of teams no player is on once setup settles.
public class TeamPruneTests {
  private const int FactionA = 200;

  [Fact]
  public void InitializeWorld_Deferred_SpawnsEveryAuthoredTeamBase() {
    var harness = SimHarness.CreateInitialized(spawnHeroesNow: false);

    // The shared map authors four team bases; all of them exist before setup completes.
    int authoredTeams = DistinctTeamCount(harness);
    authoredTeams.Should().BeGreaterThan(SimHarness.DefaultMaxPlayers,
      "the test map must author more team bases than there are players for this to be meaningful");

    harness.Count<Crystal>().Should().Be(authoredTeams);
    harness.Count<SpawnPoint>().Should().Be(authoredTeams);
    harness.Count<Turret>().Should().Be(authoredTeams * 2);
    harness.Count<Hero>().Should().Be(0, "heroes spawn only once a faction pick lands");
  }

  [Fact]
  public void TeamPrune_AfterGrace_RemovesTeamlessBases() {
    var harness = SimHarness.CreateInitialized(spawnHeroesNow: false);

    // Nobody picks: both roster players fall back to the default faction at the grace boundary,
    // which is also when teamless bases are pruned. Tick one past grace so the prune has run.
    for (int tick = 0; tick <= GraceTicks(harness); tick++)
      harness.Tick();

    harness.Count<Hero>().Should().Be(SimHarness.DefaultMaxPlayers);
    harness.Count<Crystal>().Should().Be(SimHarness.DefaultMaxPlayers);
    harness.Count<SpawnPoint>().Should().Be(SimHarness.DefaultMaxPlayers);
    harness.Count<Turret>().Should().Be(SimHarness.DefaultMaxPlayers * 2);

    // Every surviving base belongs to a team that has a player.
    var activeTeams = ActiveTeamIds(harness);
    foreach (int teamId in StructureTeamIds(harness))
      activeTeams.Should().Contain(teamId);
  }

  [Fact]
  public void TeamPrune_EarlyConfirmation_PrunesBeforeGrace() {
    var harness = SimHarness.CreateInitialized(spawnHeroesNow: false);

    // Both players confirm on the first tick, so setup is complete well before the grace window and
    // the prune should fire immediately rather than waiting for SetupGraceTicks.
    harness.Tick(
      SimHarness.SelectFactionCommand(playerId: 1, tick: 0, factionId: FactionA),
      SimHarness.SelectFactionCommand(playerId: 2, tick: 0, factionId: FactionA));

    harness.Frame.Tick.Should().BeLessThan(GraceTicks(harness));
    harness.Count<Crystal>().Should().Be(SimHarness.DefaultMaxPlayers);
    harness.Count<Turret>().Should().Be(SimHarness.DefaultMaxPlayers * 2);
    harness.Count<SpawnPoint>().Should().Be(SimHarness.DefaultMaxPlayers);
  }

  [Fact]
  public void TeamPrune_RunsBeforeFirstWave_NoTeamlessMinions() {
    var harness = SimHarness.CreateInitialized(spawnHeroesNow: false);
    var rules = harness.AssetRegistry.Get<WaveRulesAsset>();

    for (int tick = 0; tick <= rules.FirstWaveDelayTicks; tick++)
      harness.Tick();

    var activeTeams = ActiveTeamIds(harness);
    var frame = harness.Frame;
    var filter = frame.Filter<Minion, TeamComponent>();
    while (filter.Next(out var entity))
      activeTeams.Should().Contain(frame.GetReadOnly<TeamComponent>(entity).TeamId,
        "no minion should spawn for a team that was pruned");
  }

  [Fact]
  public void TeamPrune_IsOneShot_RecordedInSingleton() {
    var harness = SimHarness.CreateInitialized(spawnHeroesNow: false);

    for (int tick = 0; tick <= GraceTicks(harness); tick++)
      harness.Tick();

    harness.Count<MatchSetupState>().Should().Be(1);
    harness.Frame.TryGetSingleton<MatchSetupState>(out var entity).Should().BeTrue();
    harness.Frame.GetReadOnly<MatchSetupState>(entity).TeamlessPruned.Should().Be(1);
  }

  [Fact]
  public void TeamPrune_RaisesTeamPrunedEventForEachRemovedTeam() {
    var harness = SimHarness.CreateInitialized(spawnHeroesNow: false);
    var frame = harness.Frame;

    // Confirm both roster picks so setup completes this tick (active teams resolve to {1,2} from the
    // slots), then drive the system in isolation with a collector to capture the events it raises.
    ConfirmAllFactions(ref frame);

    var collector = new EventCollector();
    collector.BeginTick(3);
    frame.EventRaiser = collector;

    new TeamPruneSystem().Update(ref frame);

    int[] prunedTeams = collector.Collected
      .OfType<TeamPrunedEvent>()
      .Select(e => e.TeamId)
      .OrderBy(teamId => teamId)
      .ToArray();
    prunedTeams.Should().Equal(3, 4);
  }

  private static void ConfirmAllFactions(ref Frame frame) {
    var filter = frame.Filter<PlayerFaction>();
    while (filter.Next(out var entity))
      frame.Get<PlayerFaction>(entity).Confirmed = 1;
  }

  private static int DistinctTeamCount(SimHarness harness) => StructureTeamIds(harness).Count;

  private static int GraceTicks(SimHarness harness) =>
    harness.AssetRegistry.Get<MatchRulesAsset>().SetupGraceTicks;

  private static HashSet<int> StructureTeamIds(SimHarness harness) {
    var teams = new HashSet<int>();
    var frame = harness.Frame;
    var filter = frame.Filter<Crystal, TeamComponent>();
    while (filter.Next(out var entity))
      teams.Add(frame.GetReadOnly<TeamComponent>(entity).TeamId);

    return teams;
  }

  private static HashSet<int> ActiveTeamIds(SimHarness harness) {
    var teams = new HashSet<int>();
    var frame = harness.Frame;
    var filter = frame.Filter<Hero, TeamComponent>();
    while (filter.Next(out var entity))
      teams.Add(frame.GetReadOnly<TeamComponent>(entity).TeamId);

    return teams;
  }
}
