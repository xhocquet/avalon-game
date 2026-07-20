using Meesles.Avalon.Client.Scripts.View;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Godot;

namespace Meesles.Avalon;

public partial class OasisEntity : EntityViewNode {
  // This scene is also placed directly in the level (World.tscn) purely so
  // GodotFPMapLayoutExporter can read its position/MarkerType; that placement never goes through
  // the view pool, so OnInitialize/OnActivate never fire for it. Oases never despawn, so the
  // static placement and its sim-bound twin harmlessly coincide forever — but hide the static one
  // anyway to avoid a duplicate mesh/collider sitting in the scene. Stays visible in the editor.
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
    // TODO: unique oasis logic
  }
}
