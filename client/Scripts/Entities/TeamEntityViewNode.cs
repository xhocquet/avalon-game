using Godot;
using Meesles.Avalon.Client.Scripts.Interfaces;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Godot;

namespace Meesles.Avalon.Client.Scripts.Entities;

public abstract partial class TeamEntityViewNode : EntityViewNode, ISelectableTeamView {
  private const string SelectionIndicatorNode = "SelectionIndicator";
  private const uint MinimapLayer = 1u << 1;
  private const uint WorldOnlyLayer = 1u << 2;
  private const float RotationBlendRate = 37.5f;
  protected virtual Texture2D MinimapIcon => GD.Load<Texture2D>("res://Assets/UI/TargetCursor.png");
  protected virtual float MinimapPixelSize => 0.15f;
  private Quaternion _displayedRotation;
  private bool _hasDisplayedRotation;
  private HoverOutline _hoverOutline;

  public bool IsHovered => _hoverOutline?.IsHighlighted == true;

  public void SetHovered(bool hovered) {
    _hoverOutline?.SetHighlighted(hovered);
  }

  protected int TeamId { get; private set; } = -1;

  public override void _Ready() {
    if (Godot.Engine.IsEditorHint())
      return;

    SetWorldOnlyLayers(this);
    _hoverOutline = new HoverOutline(this);
    if (GetNodeOrNull<Node3D>("MinimapMarker") != null)
      return;

    AddChild(new Sprite3D {
      Name = "MinimapMarker",
      Texture = MinimapIcon,
      Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
      PixelSize = MinimapPixelSize,
      NoDepthTest = true,
      Layers = MinimapLayer
    });

    for (var ancestor = GetParent(); ancestor != null; ancestor = ancestor.GetParent()) {
      var name = ancestor.Name.ToString();
      if (name.StartsWith("Team", System.StringComparison.Ordinal) &&
          int.TryParse(name[4..], out var teamId)) {
        SetTeam(teamId);
        return;
      }
    }

    SetTeam(TeamId);
  }

  private static void SetWorldOnlyLayers(Node node) {
    if (node.Name == "MinimapMarker")
      return;

    if (node is GeometryInstance3D geometry)
      geometry.Layers = (geometry.Layers & ~1u) | WorldOnlyLayer;

    foreach (var child in node.GetChildren())
      SetWorldOnlyLayers(child);
  }

  public bool TeamMatches(int teamId) {
    return TeamId == teamId;
  }

  public override void OnLateUpdateView() {
    var target = Quaternion;
    if (!_hasDisplayedRotation || (Engine != null && Engine.HasEntityTeleported(EntityRef.Index))) {
      _displayedRotation = target;
      _hasDisplayedRotation = true;
    }
    else {
      var blend = 1f - Mathf.Exp(-RotationBlendRate * (float)GetProcessDeltaTime());
      _displayedRotation = _displayedRotation.Slerp(target, blend).Normalized();
    }

    Quaternion = _displayedRotation;
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
    SetHovered(false);
    _hasDisplayedRotation = false;
    var live = frame.Frame;
    var teamId = live != null && live.Has<Team>(EntityRef)
      ? live.GetReadOnly<Team>(EntityRef).TeamId
      : -1;
    SetTeam(teamId);
  }

  protected void ClearTeam() {
    SetHovered(false);
    _hasDisplayedRotation = false;
    SetTeam(-1);
  }
}
