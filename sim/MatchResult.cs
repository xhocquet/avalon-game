using System.Collections.Generic;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

// One player's line on the end-of-match scoreboard, read off their hero at the moment the match ended.
public readonly struct PlayerResult {
  public PlayerResult(int playerId, int teamId, int factionId, bool isWinner, int score, int heroKills,
    int deaths, int minionKills, int structureKills, int damageDealt, int level, int gold) {
    PlayerId = playerId;
    TeamId = teamId;
    FactionId = factionId;
    IsWinner = isWinner;
    Score = score;
    HeroKills = heroKills;
    Deaths = deaths;
    MinionKills = minionKills;
    StructureKills = structureKills;
    DamageDealt = damageDealt;
    Level = level;
    Gold = gold;
  }

  public int PlayerId { get; }
  public int TeamId { get; }
  public int FactionId { get; }
  public bool IsWinner { get; }
  public int Score { get; }
  public int HeroKills { get; }
  public int Deaths { get; }
  public int MinionKills { get; }
  public int StructureKills { get; }
  public int DamageDealt { get; }
  public int Level { get; }
  public int Gold { get; }
}

public readonly struct MatchResult {
  public const int NoWinnerPlayerId = -1;
  public const int NoWinnerTeamId = MatchOutcome.NoWinnerTeamId;

  public MatchResult(
    int endTick,
    int durationMs,
    int winnerPlayerId,
    int winnerTeamId,
    MatchEndReason reason,
    PlayerResult[] players
  ) {
    EndTick = endTick;
    DurationMs = durationMs;
    WinnerPlayerId = winnerPlayerId;
    WinnerTeamId = winnerTeamId;
    Reason = reason;
    Players = players;
  }

  public int EndTick { get; }
  public int DurationMs { get; }
  public int WinnerPlayerId { get; }
  public int WinnerTeamId { get; }
  public MatchEndReason Reason { get; }
  public PlayerResult[] Players { get; }

  // The team is the outcome. A win whose player id could not be resolved is still a win.
  public bool HasWinner => WinnerTeamId != NoWinnerTeamId;
  public bool IsDraw => !HasWinner;
}

public static class MatchResultReader {
  private static readonly PlayerResult[] NoPlayers = [];

  public static bool TryRead(ref Frame frame, out MatchResult result) {
    result = default;

    if (!frame.TryGetSingleton<MatchOutcome>(out var outcomeEntity))
      return false;

    ref readonly var outcome = ref frame.GetReadOnly<MatchOutcome>(outcomeEntity);
    if (!outcome.Ended)
      return false;

    // Klotho's own end state is where the single-winner player id lives; MatchOutcome owns the team.
    var winnerPlayerId = MatchResult.NoWinnerPlayerId;
    if (frame.TryGetSingleton<MatchEndStateComponent>(out var matchEndEntity))
      winnerPlayerId = frame.GetReadOnly<MatchEndStateComponent>(matchEndEntity).WinnerPlayerId;

    result = new MatchResult(
      outcome.EndTick,
      outcome.EndTick * TickMath.DeltaTimeMs(ref frame),
      winnerPlayerId,
      outcome.WinnerTeamId,
      (MatchEndReason)outcome.Reason,
      ReadPlayers(ref frame, outcome.WinnerTeamId));
    return true;
  }

  private static PlayerResult[] ReadPlayers(ref Frame frame, int winnerTeamId) {
    var players = new List<PlayerResult>();

    var filter = frame.Filter<Hero, Player, TeamComponent>();
    while (filter.Next(out var entity)) {
      ref readonly var hero = ref frame.GetReadOnly<Hero>(entity);
      ref readonly var record = ref frame.GetReadOnly<Player>(entity);
      var teamId = frame.GetReadOnly<TeamComponent>(entity).TeamId;

      players.Add(new PlayerResult(
        hero.PlayerId,
        teamId,
        frame.Has<FactionComponent>(entity) ? frame.GetReadOnly<FactionComponent>(entity).FactionId : 0,
        teamId == winnerTeamId,
        record.Score,
        record.HeroKills,
        record.Deaths,
        record.MinionKills,
        record.StructureKills,
        record.DamageDealt,
        frame.Has<ExperienceComponent>(entity) ? frame.GetReadOnly<ExperienceComponent>(entity).Level : 0,
        frame.Has<InventoryComponent>(entity) ? frame.GetReadOnly<InventoryComponent>(entity).Gold : 0));
    }

    if (players.Count == 0)
      return NoPlayers;

    // Entity iteration order is an implementation detail; the scoreboard wants a stable one.
    players.Sort(static (a, b) => a.TeamId != b.TeamId
      ? a.TeamId.CompareTo(b.TeamId)
      : a.PlayerId.CompareTo(b.PlayerId));
    return players.ToArray();
  }
}
