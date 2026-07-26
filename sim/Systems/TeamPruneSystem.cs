using System.Collections.Generic;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Logging;

namespace Meesles.Avalon;

// Culls the bases (Crystal), defenses (Turret) and minion sources (SpawnPoint) of any team that no
// player is on. InitializeWorld seeds a base for every team the map authors so the world is fully
// populated during setup; this system deletes the unclaimed ones once team assignment has settled —
// after every faction pick is confirmed, or after the shared setup grace window — and before the
// first minion wave (it is registered ahead of WaveSpawnSystem). It runs exactly once; the outcome
// is recorded in the MatchSetupState singleton so it survives rollback and never re-fires.
public class TeamPruneSystem : ISystem {
  private readonly List<int> _activeTeams = new();
  private readonly List<int> _prunedTeams = new();
  private readonly List<EntityRef> _toDestroy = new();

  public void Update(ref Frame frame) {
    if (frame.TryGetSingleton<MatchSetupState>(out var stateEntity) &&
        frame.GetReadOnly<MatchSetupState>(stateEntity).TeamlessPruned != 0)
      return;
    if (!IsSetupComplete(ref frame))
      return;

    CollectActiveTeams(ref frame);
    // Defensive: never blow away the whole map if, somehow, no team is active this tick — just wait.
    if (_activeTeams.Count == 0)
      return;

    _toDestroy.Clear();
    _prunedTeams.Clear();
    var crystals = frame.Filter<Crystal, Team>();
    while (crystals.Next(out var entity))
      AddIfTeamless(entity, ref frame);
    var turrets = frame.Filter<Turret, Team>();
    while (turrets.Next(out var entity))
      AddIfTeamless(entity, ref frame);
    var spawns = frame.Filter<SpawnPoint, Team>();
    while (spawns.Next(out var entity))
      AddIfTeamless(entity, ref frame);

    for (var i = 0; i < _toDestroy.Count; i++)
      frame.DestroyEntity(_toDestroy[i]);

    // Tell the view which teams left the match so the client can free their authored base props
    // (World.tscn Team{TeamId}). Synced event → the client acts only on the authoritative prune.
    if (frame.EventRaiser != null)
      for (var i = 0; i < _prunedTeams.Count; i++) {
        var evt = EventPool.Get<TeamPrunedEvent>();
        evt.TeamId = _prunedTeams[i];
        frame.EventRaiser.RaiseEvent(evt);
      }

    // One line on the authoritative server tells you the prune ran, when, and what it removed. On a
    // client this can log more than once because rollback re-simulates this tick — the server log is
    // the single source of truth.
    frame.Logger?.KInformation(
      $"[TeamPrune] tick={frame.Tick} activeTeams=[{string.Join(",", _activeTeams)}] " +
      $"prunedTeams=[{string.Join(",", _prunedTeams)}] prunedStructures={_toDestroy.Count}");

    MarkPruned(ref frame);
  }

  // Setup is done once no faction pick is still pending: every PlayerFaction slot is Confirmed, or
  // the grace window has elapsed (the same boundary HeroSpawnSystem uses, so heroes are spawned and
  // teams are final by now). With no slots at all — the headless spawn-heroes-now path — this is
  // trivially true from tick 0, so the harness prunes immediately.
  private static bool IsSetupComplete(ref Frame frame) {
    if (frame.Tick >= SimulationSetup.SetupGraceTicks)
      return true;

    var filter = frame.Filter<PlayerFaction>();
    while (filter.Next(out var entity))
      if (frame.GetReadOnly<PlayerFaction>(entity).Confirmed == 0)
        return false;

    return true;
  }

  // A team counts as active if it has a roster slot (PlayerFaction) or a spawned hero. The slots
  // cover the deferred lobby flow; heroes cover the spawn-heroes-now path that seeds no slots.
  private void CollectActiveTeams(ref Frame frame) {
    _activeTeams.Clear();

    var slots = frame.Filter<PlayerFaction>();
    while (slots.Next(out var entity))
      AddTeam(frame.GetReadOnly<PlayerFaction>(entity).TeamId);

    var heroes = frame.Filter<Hero, Team>();
    while (heroes.Next(out var entity))
      AddTeam(frame.GetReadOnly<Team>(entity).TeamId);
  }

  private void AddTeam(int teamId) {
    if (!_activeTeams.Contains(teamId))
      _activeTeams.Add(teamId);
  }

  private void AddIfTeamless(EntityRef entity, ref Frame frame) {
    var teamId = frame.GetReadOnly<Team>(entity).TeamId;
    if (_activeTeams.Contains(teamId))
      return;

    _toDestroy.Add(entity);
    if (!_prunedTeams.Contains(teamId))
      _prunedTeams.Add(teamId);
  }

  private static void MarkPruned(ref Frame frame) {
    if (!frame.TryGetSingleton<MatchSetupState>(out _)) {
      var entity = frame.CreateEntity();
      frame.Add(entity, new MatchSetupState { TeamlessPruned = 1 });
      return;
    }

    ref var state = ref frame.GetSingleton<MatchSetupState>();
    state.TeamlessPruned = 1;
  }
}
