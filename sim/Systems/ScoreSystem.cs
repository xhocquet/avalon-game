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
  private readonly List<int> _aliveCrystalTeamIds = [];
  private readonly List<int> _playerTeamIds = [];

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
    var matchEndTick = matchDurationMs / frame.DeltaTimeMs;
    return frame.Tick == matchEndTick;
  }

  private void EndMatch(ref Frame frame, ref MatchEndStateComponent matchEndState, int winnerPlayerId) {
    matchEndState.Ended = true;
    matchEndState.WinnerPlayerId = winnerPlayerId;

    var evt = EventPool.Get<GameOverEvent>();
    frame.EventRaiser?.RaiseEvent(evt);
  }

  private bool TryEvaluateCrystalWin(ref Frame frame, out int winnerPlayerId) {
    winnerPlayerId = NoWinnerPlayerId;
    _playerTeamIds.Clear();
    _aliveCrystalTeamIds.Clear();

    var playerFilter = frame.Filter<Player, Team>();
    while (playerFilter.Next(out var playerEntity)) {
      ref readonly var team = ref frame.GetReadOnly<Team>(playerEntity);
      if (!_playerTeamIds.Contains(team.TeamId))
        _playerTeamIds.Add(team.TeamId);
    }

    if (_playerTeamIds.Count <= 1)
      return false;

    var crystalFilter = frame.Filter<Crystal, Team>();
    while (crystalFilter.Next(out var crystalEntity)) {
      ref readonly var team = ref frame.GetReadOnly<Team>(crystalEntity);
      if (_playerTeamIds.Contains(team.TeamId) && !_aliveCrystalTeamIds.Contains(team.TeamId))
        _aliveCrystalTeamIds.Add(team.TeamId);
    }

    if (_aliveCrystalTeamIds.Count != 1)
      return false;

    return TryGetPlayerIdForTeam(ref frame, _aliveCrystalTeamIds[0], out winnerPlayerId);
  }

  private static bool TryGetPlayerIdForTeam(ref Frame frame, int teamId, out int playerId) {
    playerId = int.MaxValue;
    var filter = frame.Filter<Player, Team>();
    while (filter.Next(out var entity)) {
      ref readonly var team = ref frame.GetReadOnly<Team>(entity);
      if (team.TeamId != teamId)
        continue;

      ref readonly var player = ref frame.GetReadOnly<Player>(entity);
      if (player.PlayerId < playerId)
        playerId = player.PlayerId;
    }

    if (playerId != int.MaxValue)
      return true;

    playerId = NoWinnerPlayerId;
    return false;
  }
}
