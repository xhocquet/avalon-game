using Godot;

namespace Meesles.Avalon.Client;

[Tool]
public partial class DisconnectButton : Button {
  private readonly StyleBoxFlat _outline = new() { DrawCenter = false };

  public override void _EnterTree() {
    TeamPalette.Default.Changed += ApplyOutline;
  }

  public override void _Ready() {
    ApplyOutline();
  }

  public override void _ExitTree() {
    TeamPalette.Default.Changed -= ApplyOutline;
  }

  public override void _Draw() {
    DrawStyleBox(_outline, new Rect2(Vector2.Zero, Size));
  }

  private void ApplyOutline() {
    _outline.BorderColor = TeamPalette.Default.ForTeam(2, 0.45f);
    _outline.SetBorderWidthAll(1);
    QueueRedraw();
  }
}
