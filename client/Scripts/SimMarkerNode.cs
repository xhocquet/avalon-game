using Godot;
using Meesles.Avalon.Client.Scripts.View;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Godot;

namespace Meesles.Avalon.Client.Scripts;

[Tool]
[GlobalClass]
public partial class SimMarkerNode : EntityViewNode, ISelectableTeamView, INamedView {
  private const string UnitsGroup = "units";

  private int _teamId = -1;

  [Export] public MapMarkerType MarkerType { get; set; }
  [Export] public int Team { get; set; }

  // Generic per-marker scalar, e.g. Pickup.Amount. Only reliable when this node is the root of
  // its instanced scene (see GodotFPMapLayoutExporter.ResolveTeam for why nested overrides fail).
  [Export] public int Value { get; set; }

  public string DisplayName => MarkerType switch {
    MapMarkerType.Crystal => "Crystal",
    MapMarkerType.SpawnPoint => "Spawn Point",
    MapMarkerType.Shop => "Shop",
    MapMarkerType.Turret => "Turret",
    MapMarkerType.Oasis => "Oasis",
    MapMarkerType.Pickup => "Pickup",
    _ => MarkerType.ToString()
  };

  public bool TeamMatches(int teamId) {
    return _teamId == teamId;
  }

  public override void OnInitialize() {
    EntityViewPhysics.DisableGodotCollision(this);
  }

  public override void OnActivate(FrameRef frame) {
    var live = frame.Frame;
    if (live == null || !live.Has<UnitIdentity>(EntityRef))
      return;

    AddToGroup(UnitsGroup);
    if (live.Has<Team>(EntityRef))
      _teamId = live.GetReadOnly<Team>(EntityRef).TeamId;

    GetNodeOrNull<SelectionIndicator>("SelectionIndicator")?.SetTeamId(_teamId);
  }

  public override void OnDeactivate() {
    RemoveFromGroup(UnitsGroup);
    _teamId = -1;
  }
}
