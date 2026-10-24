using Godot;

namespace Meesles.Avalon.Client;

[Tool]
public partial class LobbyPlayerSlot : PanelContainer {
  private TeamPalette _palette;
  private bool _isEnemy;

  [Export]
  public TeamPalette Palette {
    get => _palette;
    set {
      if (_palette != null) _palette.Changed -= ApplyOutline;
      _palette = value;
      if (_palette != null && IsInsideTree()) _palette.Changed += ApplyOutline;
      ApplyOutline();
    }
  }

  [Export]
  public bool IsEnemy {
    get => _isEnemy;
    set { _isEnemy = value; ApplyOutline(); }
  }

  public override void _EnterTree() {
    if (_palette != null) _palette.Changed += ApplyOutline;
  }

  public override void _Ready() {
    ApplyOutline();
  }

  public override void _ExitTree() {
    if (_palette != null) _palette.Changed -= ApplyOutline;
  }

  private void ApplyOutline() {
    if (!IsInsideTree() || _palette == null) return;
    var style = (StyleBoxFlat)GetThemeStylebox("panel").Duplicate();
    style.BorderColor = new Color(IsEnemy ? _palette.TeamTwo : _palette.TeamOne, 0.45f);
    style.SetBorderWidthAll(1);
    AddThemeStyleboxOverride("panel", style);
  }
}
