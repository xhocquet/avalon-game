using Godot;

namespace Meesles.Avalon.Client;

[Tool, GlobalClass]
public partial class TeamPalette : Resource {
  private static TeamPalette _default;
  public static TeamPalette Default => _default ??= GD.Load<TeamPalette>("res://Assets/UI/TeamPalette.tres");

  private Color _teamOne;
  private Color _teamTwo;
  private Color _neutral;

  [Export]
  public Color TeamOne {
    get => _teamOne;
    set { _teamOne = value; EmitChanged(); }
  }

  [Export]
  public Color TeamTwo {
    get => _teamTwo;
    set { _teamTwo = value; EmitChanged(); }
  }

  [Export]
  public Color Neutral {
    get => _neutral;
    set { _neutral = value; EmitChanged(); }
  }

  public Color ForTeam(int teamId, float alpha = 1f) {
    return new Color(teamId == 1 ? TeamOne : teamId == 2 ? TeamTwo : Neutral, alpha);
  }
}
