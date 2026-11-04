using Godot;

namespace Meesles.Avalon.Client;

public partial class ActionGridPopup : Control {
  public GridContainer Grid => GetNode<GridContainer>("Container/ActionGrid");

  public override void _Ready() {
    GuiInput += input => {
      if (input is InputEventMouseButton { Pressed: true }) {
        Hide();
        AcceptEvent();
      }
    };
  }

  public override void _Input(InputEvent input) {
    if (!Visible || input is not InputEventKey { Pressed: true, Keycode: Key.Escape }) return;
    Hide();
    GetViewport().SetInputAsHandled();
  }
}
