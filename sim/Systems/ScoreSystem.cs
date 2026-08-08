using System.Collections.Generic;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon;

// Decides when the match is over and records the outcome. The outcome is team-shaped: MatchOutcome
// holds the winning team, and the single player id Klotho's MatchEndStateComponent wants is derived
// from it at the end. Nothing downstream should read the winner back out of that player id.
public class ScoreSystem : ISystem {
  private const int NoWinnerPlayerId = -1;
  private readonly List<int> _aliveCrystalTeamIds = [];

  public void Update(ref Frame frame) {
    ref var matchEndState = ref GetOrCreateMatchEndState(ref frame);
    if (matchEndState.Ended)
      return;

    if (TryEvaluateCrystalWin(ref frame, out var winnerTeamId)) {
      EndMatch(ref frame, ref matchEndState, winnerTeamId, MatchEndReason.Crystal);
      return;
    }

    if (!IsTimeoutTick(ref frame))
      return;

    EndMatch(ref frame, ref matchEndState, MatchOutcome.NoWinnerTeamId, MatchEndReason.Timeout);
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
    var matchEndTick = matchDurationMs / TickMath.DeltaTimeMs(ref frame); // floor: the tick the clock hits zero
    return frame.Tick >= matchEndTick;
  }

  private void EndMatch(ref Frame frame, ref MatchEndStateComponent matchEndState, int winnerTeamId,
    MatchEndReason reason) {
    var outcome = new MatchOutcome {
      EndTick = frame.Tick,
      WinnerTeamId = winnerTeamId,
      Reason = (int)reason
    };
    WriteOutcome(ref frame, outcome);

    // The engine's own end state only speaks in players, so give it a representative of the winning
    // team. A team that won with no hero left on the board reports a draw to Klotho and still records
    // its win in MatchOutcome.
    var winnerPlayerId = TryGetPlayerIdForTeam(ref frame, winnerTeamId, out var playerId)
      ? playerId
      : NoWinnerPlayerId;
    matchEndState.Ended = true;
    matchEndState.WinnerPlayerId = winnerPlayerId;

    var evt = EventPool.Get<GameOverEvent>();
    evt.WinnerPlayerId = winnerPlayerId;
    evt.WinnerTeamId = winnerTeamId;
    evt.Reason = (int)reason;
    frame.EventRaiser?.RaiseEvent(evt);
  }

  private static void WriteOutcome(ref Frame frame, MatchOutcome outcome) {
    if (!frame.TryGetSingleton<MatchOutcome>(out _)) {
      frame.Add(frame.CreateEntity(), outcome);
      return;
    }

    frame.GetSingleton<MatchOutcome>() = outcome;
  }

  // The win is keyed off crystals, not heroes: a team is in the match for as long as its base stands,
  // whether or not anyone is currently alive to defend it.
  private bool TryEvaluateCrystalWin(ref Frame frame, out int winnerTeamId) {
    winnerTeamId = MatchOutcome.NoWinnerTeamId;

    // Before the teamless prune the board still holds every authored base, so the count means nothing.
    if (!frame.TryGetSingleton<MatchSetupState>(out var setupEntity))
      return false;

    ref readonly var setup = ref frame.GetReadOnly<MatchSetupState>(setupEntity);
    if (setup.TeamlessPruned == 0 || setup.ContenderTeamCount < 2)
      return false;

    _aliveCrystalTeamIds.Clear();
    var crystalFilter = frame.Filter<Crystal, TeamComponent>();
    while (crystalFilter.Next(out var crystalEntity))
      TeamRegistry.AddTeam(_aliveCrystalTeamIds, frame.GetReadOnly<TeamComponent>(crystalEntity).TeamId);

    if (_aliveCrystalTeamIds.Count != 1)
      return false;

    winnerTeamId = _aliveCrystalTeamIds[0];
    return true;
  }

  private static bool TryGetPlayerIdForTeam(ref Frame frame, int teamId, out int playerId) {
    playerId = int.MaxValue;
    if (teamId == MatchOutcome.NoWinnerTeamId)
      return false;

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
