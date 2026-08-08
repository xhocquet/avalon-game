using Meesles.Avalon.Sim;

namespace Meesles.Avalon;

// One wording for the match outcome, shared by the in-game scoreboard and the lobby's one-liner so
// the two can't disagree about whether the player won.
public static class MatchResultText {
  public static string Headline(MatchResult result, int? localPlayerId) {
    if (result.IsDraw)
      return "Draw";

    // Outcome follows the team, not the player id: a teammate's win is the local player's win.
    var localTeamId = FindTeamId(result, localPlayerId);
    if (localTeamId == null)
      return $"Team {result.WinnerTeamId} wins";

    return localTeamId == result.WinnerTeamId ? "Victory" : "Defeat";
  }

  public static string Reason(MatchResult result) {
    var duration = FormatDuration(result.DurationMs);
    return result.Reason switch {
      MatchEndReason.Crystal => $"Crystal destroyed  ·  {duration}",
      MatchEndReason.Timeout => $"Time expired  ·  {duration}",
      _ => duration
    };
  }

  // Single line for surfaces with no room for a scoreboard.
  public static string Summary(MatchResult result, int? localPlayerId) {
    return $"{Headline(result, localPlayerId)}\n{Reason(result)}";
  }

  public static string FormatDuration(int durationMs) {
    var totalSeconds = durationMs / 1000;
    return $"{totalSeconds / 60:D2}:{totalSeconds % 60:D2}";
  }

  private static int? FindTeamId(MatchResult result, int? playerId) {
    if (playerId == null || result.Players == null)
      return null;

    foreach (var player in result.Players)
      if (player.PlayerId == playerId)
        return player.TeamId;

    return null;
  }
}
