using global::Godot;

namespace Meesles.Avalon {
  // View-only mapping from sim TeamId to display color. Shared by every team-tinted view
  // (bases, minions, …) so team associations render consistently.
  public static class TeamColors {
    public static Color Get(int teamId) {
      return teamId switch {
        1 => new Color(0.28f, 0.55f, 0.95f),
        2 => new Color(0.9f, 0.3f, 0.25f),
        3 => new Color(0.3f, 0.8f, 0.35f),
        4 => new Color(0.95f, 0.82f, 0.22f),
        _ => new Color(0.8f, 0.8f, 0.8f),
      };
    }
  }
}
