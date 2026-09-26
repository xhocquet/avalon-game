using Godot;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Godot;

namespace Meesles.Avalon.Client.Scripts.View;

public abstract partial class TeamEntityViewNode : EntityViewNode, ISelectableTeamView {
  private const string SelectionIndicatorNode = "SelectionIndicator";
  private const uint MinimapLayer = 1u << 1;
  private static readonly Texture2D MinimapIcon = GD.Load<Texture2D>("res://Assets/UI/target-cursor.png");
  private static readonly Color TeamOneColor = new(0.25f, 0.75f, 0.95f);
  private static readonly Color TeamTwoColor = new(0.95f, 0.35f, 0.28f);
  private static readonly Color NeutralColor = new(0.55f, 0.85f, 0.35f);

  protected int TeamId { get; private set; } = -1;

  public override void _Ready() {
    if (Godot.Engine.IsEditorHint() || GetNodeOrNull<Sprite3D>("MinimapMarker") != null)
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
    var marker = GetNodeOrNull<Sprite3D>("MinimapMarker");
    if (marker != null)
      marker.Modulate = teamId == 1 ? TeamOneColor : teamId == 2 ? TeamTwoColor : NeutralColor;
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
