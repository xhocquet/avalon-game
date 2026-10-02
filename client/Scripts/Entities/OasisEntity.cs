using xpTURN.Klotho.Core;
using xpTURN.Klotho.Godot;

namespace Meesles.Avalon.Client.Scripts.Entities;

public partial class OasisEntity : EntityViewNode {
  // Hide static map markers outside the editor.
  public override void _Ready() {
    base._Ready();
    if (Godot.Engine.IsEditorHint())
      return;

    Visible = false;
    EntityViewPhysics.DisableGodotCollision(this);
  }

  public override void OnInitialize() {
    EntityViewPhysics.DisableGodotCollision(this);
  }

  public override void OnActivate(FrameRef frame) {
    Visible = true;
  }
}
