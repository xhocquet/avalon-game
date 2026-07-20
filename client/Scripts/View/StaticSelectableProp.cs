using Godot;
using Meesles.Avalon.Client.Scripts.View;
using xpTURN.Klotho.Godot;

namespace Meesles.Avalon;

// Makes a purely-decorative, statically-placed prop click-selectable for inspection. Unlike the
// sim-bound views (Turret/Crystal/Pickup) this node never goes through the view pool, so
// OnInitialize/OnActivate never fire — the pick collider is added in _Ready instead. The prop keeps
// whatever gameplay/navigation collision it already carries (we do NOT DisableGodotCollision here);
// the selection area lives on the dedicated SelectionLayer and is queried only by InputCapture.
[Tool]
[GlobalClass]
public partial class StaticSelectableProp : EntityViewNode, INamedView {
  [Export] public string PropName { get; set; } = "Prop";

  // Selection hitbox in world metres. Leave <= 0 to auto-derive from the visible mesh bounds.
  [Export] public float SelectPickRadius { get; set; }
  [Export] public float SelectPickHeight { get; set; }

  public string DisplayName => PropName;

  public override void _Ready() {
    base._Ready();
    if (Godot.Engine.IsEditorHint())
      return;

    EntityViewPhysics.AddSelectionCollider(this, SelectPickRadius, SelectPickHeight);
  }
}
