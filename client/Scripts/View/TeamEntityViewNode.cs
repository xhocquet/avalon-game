using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Godot;

namespace Meesles.Avalon.Client.Scripts.View;

public abstract partial class TeamEntityViewNode : EntityViewNode, ISelectableTeamView {
  private const string SelectionIndicatorNode = "SelectionIndicator";

  protected int TeamId { get; private set; } = -1;

  public bool TeamMatches(int teamId) {
    return TeamId == teamId;
  }

  protected void SetTeam(int teamId) {
    TeamId = teamId;
    GetNodeOrNull<SelectionIndicator>(SelectionIndicatorNode)?.SetTeamId(TeamId);
  }

  protected void BindTeam(FrameRef frame) {
    var live = frame.Frame;
    var teamId = live != null && live.Has<Team>(EntityRef)
      ? live.GetReadOnly<Team>(EntityRef).TeamId
      : -1;
    SetTeam(teamId);
  }

  protected void ClearTeam() {
    SetTeam(-1);
  }
}
