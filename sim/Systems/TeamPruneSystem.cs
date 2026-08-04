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
  private readonly List<int> _activeTeams = [];
  private readonly List<int> _prunedTeams = [];
  private readonly List<EntityRef> _toDestroy = [];

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
    var crystals = frame.Filter<Crystal, TeamComponent>();
    while (crystals.Next(out var entity))
      AddIfTeamless(entity, ref frame);
    var turrets = frame.Filter<Turret, TeamComponent>();
    while (turrets.Next(out var entity))
      AddIfTeamless(entity, ref frame);
    var spawns = frame.Filter<SpawnPoint, TeamComponent>();
    while (spawns.Next(out var entity))
      AddIfTeamless(entity, ref frame);

    foreach (var t in _toDestroy)
      frame.DestroyEntity(t);

    if (frame.EventRaiser != null)
      foreach (var teamId in _prunedTeams) {
        var evt = EventPool.Get<TeamPrunedEvent>();
        evt.TeamId = teamId;
        frame.EventRaiser.RaiseEvent(evt);
      }

    frame.Logger.KInformation(
      $"[TeamPrune] tick={frame.Tick} activeTeams=[{string.Join(",", _activeTeams)}] " +
      $"prunedTeams=[{string.Join(",", _prunedTeams)}] prunedStructures={_toDestroy.Count}");

    MarkPruned(ref frame);
  }

  // Setup is done once no faction pick is still pending, or time limit expires
  private static bool IsSetupComplete(ref Frame frame) {
    if (frame.Tick >= SimulationSetup.GetSetupGraceTicks(ref frame))
      return true;

    var filter = frame.Filter<PlayerFaction>();
    while (filter.Next(out var entity))
      if (frame.GetReadOnly<PlayerFaction>(entity).Confirmed == 0)
        return false;

    return true;
  }

  private void CollectActiveTeams(ref Frame frame) {
    TeamRegistry.CollectActiveTeams(ref frame, _activeTeams);

    // Pruning can run before every hero has spawned, so a confirmed slot also holds a team open.
    var slots = frame.Filter<PlayerFaction>();
    while (slots.Next(out var entity))
      TeamRegistry.AddTeam(_activeTeams, frame.GetReadOnly<PlayerFaction>(entity).TeamId);
  }

  private void AddIfTeamless(EntityRef entity, ref Frame frame) {
    var teamId = frame.GetReadOnly<TeamComponent>(entity).TeamId;
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
