using Godot;
using Meesles.Avalon.Client.Scripts.GameState;
using Meesles.Avalon.Client.Scripts.Interfaces;
using Meesles.Avalon.Client.Scripts.View;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Godot;

namespace Meesles.Avalon.Client.Scripts.Entities;

public partial class TurretEntity : TeamEntityViewNode, IAttackableView, INamedView {
  private const string UnitsGroup = "units";
  private const string CooldownParam = "fill_value";
  private const string AoeColorParam = "aoe_color";

  public string DisplayName => "Turret";
  protected override Texture2D MinimapIcon => GD.Load<Texture2D>("res://Assets/Portraits/Minimap/Turret.webp");
  protected override float MinimapPixelSize => 0.014875f;

  [Export] public float SelectPickRadius { get; set; } = 1.2f;
  [Export] public float SelectPickHeight { get; set; } = 4.5f;
  [Export] public float HealthBarHeight { get; set; } = 4.9f;
  [Export] public PackedScene ArrowVolleyScene { get; set; }

  private MeshInstance3D _loadingIndicator;
  private ShaderMaterial _loadingIndicatorMaterial;
  private TurretArrowVolley _arrowVolley;
  private Basis _authoredBasis;

  public bool OnAttackWindupVfx(Vector3 targetPosition, float windupSeconds) {
    return PlayArrowVolley(targetPosition, windupSeconds, null);
  }

  public bool PlayArrowVolley(Vector3 targetPosition, float windupSeconds, EntityViewNode target) {
    OnAttackCanceledVfx();
    var launchPoints = GetNodeOrNull<Node3D>("ArrowLaunchPoints");
    if (ArrowVolleyScene == null || launchPoints == null || launchPoints.GetChildCount() == 0) return false;
    var origins = new Vector3[launchPoints.GetChildCount()];
    for (var i = 0; i < origins.Length; i++)
      origins[i] = launchPoints.GetChild<Node3D>(i).GlobalPosition;
    _arrowVolley = ArrowVolleyScene.Instantiate<TurretArrowVolley>();
    AddChild(_arrowVolley);
    _arrowVolley.Start(origins, targetPosition, windupSeconds, target);
    return true;
  }

  public void OnAttackCanceledVfx() {
    if (IsInstanceValid(_arrowVolley)) {
      _arrowVolley.Visible = false;
      _arrowVolley.QueueFree();
    }
    _arrowVolley = null;
  }

  public void OnHitVfx(float damage, Vector3 attackerPosition) {
  }

  public override void OnInitialize() {
    EntityViewPhysics.DisableGodotCollision(this);
    EntityViewPhysics.AddSelectionCollider(this, SelectPickRadius, SelectPickHeight);

    _loadingIndicator = GetNodeOrNull<MeshInstance3D>("LoadingIndicator");
    if (_loadingIndicator != null) {
      _loadingIndicator.Visible = false;
      // Duplicate the shared material before tinting it.
      if ((_loadingIndicator.Mesh as PrimitiveMesh)?.Material is ShaderMaterial source) {
        _loadingIndicatorMaterial = (ShaderMaterial)source.Duplicate();
        _loadingIndicator.SetSurfaceOverrideMaterial(0, _loadingIndicatorMaterial);
      }
    }
  }

  public override void OnActivate(FrameRef frame) {
    _authoredBasis = Basis;
    AddToGroup(UnitsGroup);

    var live = frame.Frame;
    if (live != null && live.Has<UnitIdentity>(EntityRef))
      SetCachedUnitId(live.GetReadOnly<UnitIdentity>(EntityRef).UnitId);
    BindTeam(frame);
    ApplyTeamTint();
  }

  public override void OnDeactivate() {
    OnAttackCanceledVfx();
    RemoveFromGroup(UnitsGroup);
    ClearTeam();
  }

  public override void OnLateUpdateView() {
    Basis = _authoredBasis;
  }

  public override void OnUpdateView() {
    if (_loadingIndicator == null || Engine == null) return;

    var frame = Engine.PredictedFrame.Frame;
    if (frame == null || !frame.Has<Combat>(EntityRef)) {
      _loadingIndicator.Visible = false;
      return;
    }

    _loadingIndicator.Visible = frame.GetReadOnly<Combat>(EntityRef).TargetUnitId != 0;
    if (!_loadingIndicator.Visible) return;

    _loadingIndicatorMaterial?.SetShaderParameter(CooldownParam,
      CombatView.CooldownProgress(frame, EntityRef));
  }

  private void ApplyTeamTint() {
    var color = TeamPalette.Default.ForTeam(TeamId, 0.92f);
    _loadingIndicatorMaterial?.SetShaderParameter(AoeColorParam, color);
  }
}
