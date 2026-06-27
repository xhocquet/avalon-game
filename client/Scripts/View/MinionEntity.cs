using xpTURN.Klotho.Core;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Godot;
using Meesles.Avalon.Sim.Models;

namespace Meesles.Avalon {
  public partial class MinionEntity : EntityViewNode, ISelectableTeamView {
    private const string UnitsGroup = "units";

    private int _ownerId = -1;
    private int _teamId = -1;

    public override void OnActivate(FrameRef frame) {
      AddToGroup(UnitsGroup);

      var live = frame.Frame;
      if (live != null && live.Has<OwnerComponent>(EntityRef))
        _ownerId = live.GetReadOnly<OwnerComponent>(EntityRef).OwnerId;
      if (live != null && live.Has<Team>(EntityRef))
        _teamId = live.GetReadOnly<Team>(EntityRef).TeamId;
    }

    public override void OnDeactivate() {
      RemoveFromGroup(UnitsGroup);
      _ownerId = -1;
      _teamId = -1;
    }

    public override bool OwnerMatches(int ownerId) => _ownerId == ownerId;
    public bool TeamMatches(int teamId) => _teamId == teamId;
  }
}
