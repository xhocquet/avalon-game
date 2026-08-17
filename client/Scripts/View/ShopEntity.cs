using Godot;
using Meesles.Avalon.Client.Scripts.View;
using xpTURN.Klotho.Godot;

namespace Meesles.Avalon;

// View entity for the team shops. Unlike the pooled, sim-bound views (Crystal/Turret) the shop is
// placed statically in the level (World.tscn, under each TeamN folder) and never goes through the
// view pool, so OnInitialize/OnActivate never fire — the pick collider and team wiring are set up in
// _Ready instead (same lifecycle as StaticSelectableProp, and the legacy SimMarkerNode selection
// hack this replaces). A nested "SimMarker" child still drives map-layout export; this root only
// owns the view/selection/logic. Gameplay collision authored on the glb is left intact (we do NOT
// DisableGodotCollision here).
[Tool]
[GlobalClass]
public partial class ShopEntity : TeamEntityViewNode, INamedView {
  // Shops live in World.tscn rather than under the view root, so the open-shop hotkey can't reach them
  // by walking pooled views the way box-select does.
  public const string ShopsGroup = "shops";

  public string DisplayName => "Shop";

  // Owning team for this shop. Set per-instance in World.tscn; the SimMarker export derives its own
  // team from the TeamN folder independently. -1 leaves the shop team-neutral for selection. Unlike
  // the pooled sim-bound views, the shop's team comes from the editor rather than the frame, so it
  // feeds SetTeam directly in _Ready instead of calling BindTeam.
  [Export] public int Team { get; set; } = -1;

  // Selection hitbox in world metres. Leave <= 0 to auto-derive from the visible mesh bounds (the
  // shop is a static, non-skinned mesh so its AABB is reliable); set explicitly to fine-tune.
  [Export] public float SelectPickRadius { get; set; } = -1.0f;
  [Export] public float SelectPickHeight { get; set; } = -1.0f;

  public override void _Ready() {
    base._Ready();
    if (Godot.Engine.IsEditorHint())
      return;

    EntityViewPhysics.AddSelectionCollider(this, SelectPickRadius, SelectPickHeight);
    AddToGroup(ShopsGroup);
    SetTeam(Team);
  }
}
