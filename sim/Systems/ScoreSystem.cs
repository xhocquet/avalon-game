using System.Collections.Generic;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon;

public class ScoreSystem : ISystem {
  private const int NoWinnerPlayerId = -1;
  private readonly List<int> _activeTeamIds = [];
  private readonly List<int> _aliveCrystalTeamIds = [];

  public void Update(ref Frame frame) {
    ref var matchEndState = ref GetOrCreateMatchEndState(ref frame);
    if (matchEndState.Ended)
      return;

    if (TryEvaluateCrystalWin(ref frame, out var winnerPlayerId)) {
      EndMatch(ref frame, ref matchEndState, winnerPlayerId);
      return;
    }

    if (!IsTimeoutTick(ref frame))
      return;

    EndMatch(ref frame, ref matchEndState, NoWinnerPlayerId);
  }

  private static ref MatchEndStateComponent GetOrCreateMatchEndState(ref Frame frame) {
    if (!frame.TryGetSingleton<MatchEndStateComponent>(out _)) {
      var entity = frame.CreateEntity();
      frame.Add(entity, new MatchEndStateComponent {
        Ended = false,
        WinnerPlayerId = NoWinnerPlayerId
      });
    }

    return ref frame.GetSingleton<MatchEndStateComponent>();
  }

  private static bool IsTimeoutTick(ref Frame frame) {
    var rules = frame.AssetRegistry.Get<MatchRulesAsset>();

    var matchDurationMs = (rules.MatchDuration * FP64.FromInt(1000)).ToInt();
    var deltaTimeMs = frame.DeltaTimeMs > 0 ? frame.DeltaTimeMs : 16;
    var matchEndTick = matchDurationMs / deltaTimeMs;
    return frame.Tick >= matchEndTick;
  }

  private void EndMatch(ref Frame frame, ref MatchEndStateComponent matchEndState, int winnerPlayerId) {
    matchEndState.Ended = true;
    matchEndState.WinnerPlayerId = winnerPlayerId;

    var evt = EventPool.Get<GameOverEvent>();
    frame.EventRaiser?.RaiseEvent(evt);
  }

  private bool TryEvaluateCrystalWin(ref Frame frame, out int winnerPlayerId) {
    winnerPlayerId = NoWinnerPlayerId;
    _aliveCrystalTeamIds.Clear();

    TeamRegistry.CollectActiveTeams(ref frame, _activeTeamIds);
    if (_activeTeamIds.Count <= 1)
      return false;

    var crystalFilter = frame.Filter<Crystal, TeamComponent>();
    while (crystalFilter.Next(out var crystalEntity)) {
      ref readonly var team = ref frame.GetReadOnly<TeamComponent>(crystalEntity);
      if (_activeTeamIds.Contains(team.TeamId) && !_aliveCrystalTeamIds.Contains(team.TeamId))
        _aliveCrystalTeamIds.Add(team.TeamId);
    }

    if (_aliveCrystalTeamIds.Count != 1)
      return false;

    return TryGetPlayerIdForTeam(ref frame, _aliveCrystalTeamIds[0], out winnerPlayerId);
  }

  private static bool TryGetPlayerIdForTeam(ref Frame frame, int teamId, out int playerId) {
    playerId = int.MaxValue;
    var filter = frame.Filter<Hero, TeamComponent>();
    while (filter.Next(out var entity)) {
      ref readonly var team = ref frame.GetReadOnly<TeamComponent>(entity);
      if (team.TeamId != teamId)
        continue;

      ref readonly var hero = ref frame.GetReadOnly<Hero>(entity);
      if (hero.PlayerId < playerId)
        playerId = hero.PlayerId;
    }

    if (playerId != int.MaxValue)
      return true;

    playerId = NoWinnerPlayerId;
    return false;
  }
}
