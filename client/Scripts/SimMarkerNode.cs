using Godot;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Models;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Godot;

namespace Meesles.Avalon {
  [Tool]
  [GlobalClass]
  public partial class SimMarkerNode : EntityViewNode, ISelectableTeamView {
    private const string UnitsGroup = "units";

    [Export] public MapMarkerType MarkerType { get; set; }
    [Export] public int Team { get; set; }

    private int _teamId = -1;

    public override void OnInitialize() {
      EntityViewPhysics.DisableGodotCollision(this);
    }

    public override void OnActivate(FrameRef frame) {
      var live = frame.Frame;
      if (live == null || !live.Has<Unit>(EntityRef))
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

    public bool TeamMatches(int teamId) => _teamId == teamId;
  }
}
