namespace Meesles.Avalon.Client;

public readonly record struct MatchTickerMessage(
  MatchTimelineParticipant Actor, string Text, MatchTimelineParticipant Target = default);

public static class MatchTickerTemplates {
  public static MatchTickerMessage Resolve(MatchTimelineEntry entry) => entry.Kind switch {
    MatchTimelineKind.PlayerDefeated when entry.Actor.IsHero => new(entry.Actor, "defeated", entry.Target),
    MatchTimelineKind.PlayerDefeated => new(entry.Target, "defeated!"),
    MatchTimelineKind.TurretDestroyed when entry.Target.IsHero => new(Owner(entry.Target), "turret destroyed."),
    MatchTimelineKind.TurretDestroyed => new(default, "Turret destroyed."),
    MatchTimelineKind.CrystalDestroyed when entry.Target.IsHero => new(Owner(entry.Target), "base destroyed."),
    MatchTimelineKind.CrystalDestroyed => new(default, "Base destroyed."),
    MatchTimelineKind.MatchEnded => new(default, entry.TeamId > 0 ? $"Team {entry.TeamId} wins!" : "Match ended in a draw."),
    _ => new(default, entry.Kind.ToString())
  };

  private static MatchTimelineParticipant Owner(MatchTimelineParticipant owner) => owner with { Name = $"{owner.Name}'s" };
}
