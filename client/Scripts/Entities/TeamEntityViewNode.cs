using Godot;
using Meesles.Avalon.Client.Scripts.Interfaces;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Godot;

namespace Meesles.Avalon.Client.Scripts.Entities;

public abstract partial class TeamEntityViewNode : EntityViewNode, ISelectableTeamView {
  private const string SelectionIndicatorNode = "SelectionIndicator";
  private const uint MinimapLayer = 1u << 1;
  private static Texture2D MinimapIcon => GD.Load<Texture2D>("res://Assets/UI/TargetCursor.png");

  protected int TeamId { get; private set; } = -1;

  public override void _Ready() {
    if (Godot.Engine.IsEditorHint() || GetNodeOrNull<Node3D>("MinimapMarker") != null)
      return;

    AddChild(new Sprite3D {
      Name = "MinimapMarker",
      Texture = MinimapIcon,
      Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
      PixelSize = 0.15f,
      NoDepthTest = true,
      Layers = MinimapLayer
    });
  }

  public bool TeamMatches(int teamId) {
    return TeamId == teamId;
  }

  protected void SetTeam(int teamId) {
    TeamId = teamId;
    var marker = GetNodeOrNull<Node3D>("MinimapMarker");
    if (marker is MinimapHeroMarker portrait)
      portrait.SetTeamColor(TeamPalette.Default.ForTeam(teamId));
    else if (marker is Sprite3D sprite)
      sprite.Modulate = TeamPalette.Default.ForTeam(teamId);
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
