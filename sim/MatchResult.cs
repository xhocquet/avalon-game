using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

public readonly struct MatchResult {
  public const int NoWinnerPlayerId = -1;
  public const int NoWinnerTeamId = -1;

  public MatchResult(
    int endTick,
    int winnerPlayerId,
    int winnerTeamId,
    MatchEndReason reason
  ) {
    EndTick = endTick;
    WinnerPlayerId = winnerPlayerId;
    WinnerTeamId = winnerTeamId;
    Reason = reason;
  }

  public int EndTick { get; }
  public int WinnerPlayerId { get; }
  public int WinnerTeamId { get; }
  public MatchEndReason Reason { get; }
  public bool HasWinner => WinnerPlayerId != NoWinnerPlayerId;
  public bool IsDraw => !HasWinner;
}

public static class MatchResultReader {
  public static bool TryRead(ref Frame frame, int endTick, out MatchResult result) {
    result = default;

    if (!frame.TryGetSingleton<MatchEndStateComponent>(out var matchEndEntity))
      return false;

    ref readonly var matchEnd = ref frame.GetReadOnly<MatchEndStateComponent>(matchEndEntity);
    if (!matchEnd.Ended)
      return false;

    // A winner whose hero entity is already gone stays team-less rather than falling back to team 0.
    var winnerTeamId = MatchResult.NoWinnerTeamId;
    if (matchEnd.WinnerPlayerId != MatchResult.NoWinnerPlayerId &&
        UnitLookup.TryGetPlayerTeamId(ref frame, matchEnd.WinnerPlayerId, out var teamId))
      winnerTeamId = teamId;

    var reason = matchEnd.WinnerPlayerId == MatchResult.NoWinnerPlayerId
      ? MatchEndReason.Timeout
      : MatchEndReason.Crystal;

    result = new MatchResult(endTick, matchEnd.WinnerPlayerId, winnerTeamId, reason);
    return true;
  }
}
