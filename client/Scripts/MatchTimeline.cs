using System;
using System.Collections.Generic;
using Meesles.Avalon.Sim;
using xpTURN.Klotho.Core;

namespace Meesles.Avalon.Client;

public enum MatchTimelineKind {
  PlayerDefeated,
  TurretDestroyed,
  CrystalDestroyed,
  MatchEnded
}

public readonly record struct MatchTimelineEntry(
  int Sequence, int Tick, MatchTimelineKind Kind,
  int PlayerId = 0, int UnitId = 0, int TeamId = 0, int DestroyerUnitId = 0, int Reason = 0,
  MatchTimelineParticipant Actor = default, MatchTimelineParticipant Target = default);

public readonly record struct MatchTimelineParticipant(int PlayerId, string Name, int FactionId, int TeamId) {
  public bool IsHero => FactionId != 0 && !string.IsNullOrWhiteSpace(Name);
}

public sealed class MatchTimeline {
  private readonly List<MatchTimelineEntry> _entries = new();
  public IReadOnlyList<MatchTimelineEntry> Entries { get; }
  public event Action Changed;
  public Func<int, string> ResolvePlayerName { get; set; }

  public MatchTimeline() {
    Entries = _entries.AsReadOnly();
  }

  internal void RecordConfirmed(SimulationEvent evt) {
    var sequence = _entries.Count + 1;
    MatchTimelineEntry? entry = evt switch {
      PlayerDiedEvent died => new(sequence, died.Tick, MatchTimelineKind.PlayerDefeated,
        PlayerId: died.PlayerId, UnitId: died.UnitId, TeamId: died.TeamId, DestroyerUnitId: died.DestroyerUnitId,
        Actor: Participant(died.DestroyerPlayerId, died.DestroyerFactionId, died.DestroyerTeamId),
        Target: Participant(died.PlayerId, died.FactionId, died.TeamId)),
      TurretDestroyedEvent turret => new(sequence, turret.Tick, MatchTimelineKind.TurretDestroyed,
        UnitId: turret.UnitId, TeamId: turret.TeamId, DestroyerUnitId: turret.DestroyerUnitId,
        Actor: Participant(turret.DestroyerPlayerId, turret.DestroyerFactionId, turret.DestroyerTeamId),
        Target: Participant(turret.PlayerId, turret.FactionId, turret.TeamId)),
      CrystalDestroyedEvent crystal => new(sequence, crystal.Tick, MatchTimelineKind.CrystalDestroyed,
        UnitId: crystal.UnitId, TeamId: crystal.TeamId, DestroyerUnitId: crystal.DestroyerUnitId,
        Actor: Participant(crystal.DestroyerPlayerId, crystal.DestroyerFactionId, crystal.DestroyerTeamId),
        Target: Participant(crystal.PlayerId, crystal.FactionId, crystal.TeamId)),
      GameOverEvent ended => new(sequence, ended.Tick, MatchTimelineKind.MatchEnded,
        PlayerId: ended.WinnerPlayerId, TeamId: ended.WinnerTeamId, Reason: ended.Reason),
      _ => null
    };
    if (entry is not { } value) return;

    _entries.Add(value); // Copy payload values; simulation events are pooled.
    Changed?.Invoke();
  }

  private MatchTimelineParticipant Participant(int playerId, int factionId, int teamId) {
    if (factionId == 0 || playerId == UnitLookup.NoPlayerId) return default;
    var name = ResolvePlayerName?.Invoke(playerId);
    return new(playerId, string.IsNullOrWhiteSpace(name) ? $"Player {playerId}" : name, factionId, teamId);
  }

  internal void Clear() {
    _entries.Clear();
    Changed?.Invoke();
  }
}
