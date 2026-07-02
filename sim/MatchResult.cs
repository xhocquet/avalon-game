using Meesles.Avalon.Sim.Models;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim {
  public enum MatchEndReason {
    Unknown = 0,
    Crystal = 1,
    Timeout = 2,
  }

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

      int winnerTeamId = MatchResult.NoWinnerTeamId;
      if (matchEnd.WinnerPlayerId != MatchResult.NoWinnerPlayerId)
        winnerTeamId = GetTeamIdForPlayer(ref frame, matchEnd.WinnerPlayerId);

      var reason = matchEnd.WinnerPlayerId == MatchResult.NoWinnerPlayerId
        ? MatchEndReason.Timeout
        : MatchEndReason.Crystal;

      result = new MatchResult(endTick, matchEnd.WinnerPlayerId, winnerTeamId, reason);
      return true;
    }

    private static int GetTeamIdForPlayer(ref Frame frame, int playerId) {
      var filter = frame.Filter<Player, Team>();
      while (filter.Next(out var entity)) {
        ref readonly var player = ref frame.GetReadOnly<Player>(entity);
        if (player.PlayerId != playerId)
          continue;

        ref readonly var team = ref frame.GetReadOnly<Team>(entity);
        return team.TeamId;
      }

      return MatchResult.NoWinnerTeamId;
    }
  }
}
