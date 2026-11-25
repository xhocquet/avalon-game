using Godot;
using Meesles.Avalon.Client.Scripts.Interfaces;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Assets;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Client.Scripts.Entities;

// Static shops initialize selection and team state in _Ready.
[Tool]
[GlobalClass]
public partial class ShopEntity : TeamEntityViewNode, INamedView {
  // Hotkeys find static shops through this group.
  public const string ShopsGroup = "shops";

  public string DisplayName => "Shop";
  protected override Texture2D MinimapIcon => GD.Load<Texture2D>("res://Assets/Portraits/Minimap/Shop.webp");
  protected override float MinimapPixelSize => 0.075f;

  // Static shops get their team from World.tscn.
  [Export] public int Team { get; set; } = -1;

  // Selection size in world metres.
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

  public void SyncMap(Frame frame, MapLayoutAsset map, int localTeamId, bool noFog) {
    var position = GlobalPosition;
    for (var i = 0; i < map.MarkerCount; i++) {
      if (map.MarkerTypes[i] != (int)MapMarkerType.Shop ||
          map.MarkerPositions[i].ToVector3().DistanceSquaredTo(position) >= 0.01f) continue;
      Team = map.MarkerTeams[i];
      if (!TeamMatches(Team)) SetTeam(Team);
      Visible = noFog || Team == localTeamId ||
        Visibility.IsPositionVisible(ref frame, localTeamId, map.MarkerPositions[i].ToXZ());
      return;
    }
    Visible = false;
  }
}
