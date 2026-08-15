using Godot;
using Meesles.Avalon.Client.Scripts.View;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Godot;

namespace Meesles.Avalon;

public partial class TurretEntity : TeamEntityViewNode, IAttackableView, INamedView {
  private const string UnitsGroup = "units";
  private const string CooldownParam = "fill_value";
  private const string AoeColorParam = "aoe_color";

  // Team tints for the focus/attack ring, matching SelectionIndicator's palette. Alpha carries the
  // ring opacity the authored material shipped with.
  private static readonly Color TeamOneColor = new(0.25f, 0.75f, 0.95f, 0.92f);
  private static readonly Color TeamTwoColor = new(0.95f, 0.35f, 0.28f, 0.92f);
  private static readonly Color NeutralColor = new(0.55f, 0.85f, 0.35f, 0.92f);

  public string DisplayName => "Turret";

  [Export] public float SelectPickRadius { get; set; } = 1.2f;
  [Export] public float SelectPickHeight { get; set; } = 4.5f;

  private MeshInstance3D _loadingIndicator;
  private ShaderMaterial _loadingIndicatorMaterial;

  public bool OnAttackVfx(Vector3 targetPosition) {
    // TODO: turret fire animation / particles
    return false;
  }

  public void OnHitVfx(float damage, Vector3 attackerPosition) {
    // TODO: hit reaction / particles
  }

  public override void OnInitialize() {
    EntityViewPhysics.DisableGodotCollision(this);
    EntityViewPhysics.AddSelectionCollider(this, SelectPickRadius, SelectPickHeight);

    _loadingIndicator = GetNodeOrNull<MeshInstance3D>("LoadingIndicator");
    if (_loadingIndicator != null) {
      _loadingIndicator.Visible = false;
      // The shader material lives on the shared PlaneMesh sub-resource, so duplicate it into a
      // per-instance surface override — otherwise team tinting one turret repaints them all.
      if ((_loadingIndicator.Mesh as PrimitiveMesh)?.Material is ShaderMaterial source) {
        _loadingIndicatorMaterial = (ShaderMaterial)source.Duplicate();
        _loadingIndicator.SetSurfaceOverrideMaterial(0, _loadingIndicatorMaterial);
      }
    }
  }

  public override void OnActivate(FrameRef frame) {
    AddToGroup(UnitsGroup);

    var live = frame.Frame;
    if (live != null && live.Has<UnitIdComponent>(EntityRef))
      SetCachedUnitId(live.GetReadOnly<UnitIdComponent>(EntityRef).UnitId);
    BindTeam(frame);
    ApplyTeamTint();
  }

  public override void OnDeactivate() {
    RemoveFromGroup(UnitsGroup);
    ClearTeam();
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
    var color = TeamId == 1 ? TeamOneColor : TeamId == 2 ? TeamTwoColor : NeutralColor;
    _loadingIndicatorMaterial?.SetShaderParameter(AoeColorParam, color);
  }
}
