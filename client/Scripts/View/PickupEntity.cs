using Godot;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Godot;

namespace Meesles.Avalon;

public partial class PickupEntity : EntityViewNode, INamedView {
  // Selection hitbox in world metres, sized to the standing water bottle. Leave <= 0 to auto-derive.
  [Export] public float SelectPickRadius { get; set; } = 0.4f;
  [Export] public float SelectPickHeight { get; set; } = 1.4f;

  public string DisplayName => "Water Bottle";

  // This scene also gets placed directly in the level (World.tscn) purely so
  // GodotFPMapLayoutExporter can read its position/MarkerType/Value; those placements never go
  // through the view pool, so OnInitialize/OnActivate never fire for them. Without this, a
  // placement stays visible forever even after its sim-bound twin is collected and destroyed.
  // Hide + de-collide by default and only reveal once actually bound to a live Pickup entity.
  // Stays visible in the editor so designers can still see where pickups are placed.
  public override void _Ready() {
    base._Ready();
    if (Godot.Engine.IsEditorHint())
      return;

    Visible = false;
    EntityViewPhysics.DisableGodotCollision(this);
  }

  public override void OnInitialize() {
    EntityViewPhysics.DisableGodotCollision(this);
    EntityViewPhysics.AddSelectionCollider(this, SelectPickRadius, SelectPickHeight);
  }

  public override void OnActivate(FrameRef frame) {
    Visible = true;
    // TODO: bob/spin idle animation, VFX on collect
  }
}
