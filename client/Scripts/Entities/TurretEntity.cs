using Godot;
using Meesles.Avalon.Client.Scripts.GameState;
using Meesles.Avalon.Client.Scripts.View;
using Meesles.Avalon.Client.Scripts.Interfaces;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Core;

namespace Meesles.Avalon.Client.Scripts.Entities;

public partial class TurretEntity : TeamEntityViewNode, IAttackableView, INamedView {
  private const string UnitsGroup = "units";
  private const string CooldownParam = "fill_value";
  private const string AoeColorParam = "aoe_color";

  // Match SelectionIndicator colors and authored opacity.
  private static readonly Color TeamOneColor = new(0.25f, 0.75f, 0.95f, 0.92f);
  private static readonly Color TeamTwoColor = new(0.95f, 0.35f, 0.28f, 0.92f);
  private static readonly Color NeutralColor = new(0.55f, 0.85f, 0.35f, 0.92f);

  public string DisplayName => "Turret";

  [Export] public float SelectPickRadius { get; set; } = 1.2f;
  [Export] public float SelectPickHeight { get; set; } = 4.5f;

  private MeshInstance3D _loadingIndicator;
  private ShaderMaterial _loadingIndicatorMaterial;

  public bool OnAttackWindupVfx(Vector3 targetPosition, float windupSeconds) {
    return false;
  }

  public void OnAttackCanceledVfx() { }

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
    AddToGroup(UnitsGroup);

    var live = frame.Frame;
    if (live != null && live.Has<UnitIdentity>(EntityRef))
      SetCachedUnitId(live.GetReadOnly<UnitIdentity>(EntityRef).UnitId);
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
