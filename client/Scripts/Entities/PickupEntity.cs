using Godot;
using Meesles.Avalon.Client.Scripts.Interfaces;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Godot;

namespace Meesles.Avalon.Client.Scripts.Entities;

public partial class PickupEntity : EntityViewNode, INamedView {
  // Selection size in world metres.
  [Export] public float SelectPickRadius { get; set; } = 0.4f;
  [Export] public float SelectPickHeight { get; set; } = 1.4f;

  public string DisplayName => "Water Bottle";

  // Keep static map markers hidden until pooled at runtime.
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
  }
}
