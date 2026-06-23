using global::Godot;

namespace Meesles.Avalon {
  public partial class SelectionIndicator : Node3D {
    public override void _Ready() {
      if (!Engine.IsEditorHint())
        Visible = false;
    }

    public void SetSelected(bool selected) {
      Visible = selected;
    }
  }
}
