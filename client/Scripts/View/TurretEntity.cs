using Godot;
using Meesles.Avalon.Sim.Models;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Godot;

namespace Meesles.Avalon;

public partial class TurretEntity : EntityViewNode, ISelectableTeamView, IAttackableView, INamedView {
  private const string UnitsGroup = "units";
  private const string CooldownParam = "fill_value";

  public string DisplayName => "Turret";

  [Export] public float SelectPickRadius { get; set; } = 1.2f;
  [Export] public float SelectPickHeight { get; set; } = 4.5f;

  private int _teamId = -1;
  private MeshInstance3D _loadingIndicator;
  private ShaderMaterial _loadingIndicatorMaterial;

  public void OnAttackVfx(Vector3 targetPosition) {
    // TODO: turret fire animation / particles
  }

  public void OnHitVfx(int damage, Vector3 attackerPosition) {
    // TODO: hit reaction / particles
  }

  public bool TeamMatches(int teamId) {
    return _teamId == teamId;
  }

  public override void OnInitialize() {
    EntityViewPhysics.DisableGodotCollision(this);
    EntityViewPhysics.AddSelectionCollider(this, SelectPickRadius, SelectPickHeight);

    _loadingIndicator = GetNodeOrNull<MeshInstance3D>("LoadingIndicator");
    _loadingIndicatorMaterial = (_loadingIndicator?.Mesh as PrimitiveMesh)?.Material as ShaderMaterial;
    if (_loadingIndicator != null)
      _loadingIndicator.Visible = false;
  }

  public override void OnActivate(FrameRef frame) {
    AddToGroup(UnitsGroup);

    var live = frame.Frame;
    if (live != null && live.Has<Unit>(EntityRef))
      SetCachedUnitId(live.GetReadOnly<Unit>(EntityRef).UnitId);
    if (live != null && live.Has<Team>(EntityRef))
      _teamId = live.GetReadOnly<Team>(EntityRef).TeamId;

    GetNodeOrNull<SelectionIndicator>("SelectionIndicator")?.SetTeamId(_teamId);
  }

  public override void OnDeactivate() {
    RemoveFromGroup(UnitsGroup);
    _teamId = -1;
  }

  public override void OnUpdateView() {
    if (_loadingIndicator == null || Engine == null) return;

    var frame = Engine.PredictedFrame.Frame;
    if (frame == null || !frame.Has<Combat>(EntityRef)) {
      _loadingIndicator.Visible = false;
      return;
    }

    ref readonly var combat = ref frame.GetReadOnly<Combat>(EntityRef);
    _loadingIndicator.Visible = combat.Target.IsValid;
    if (!_loadingIndicator.Visible || combat.AttackCooldownTicks <= 0) return;

    var progress = 1f - (float)combat.CooldownRemainingTicks / combat.AttackCooldownTicks;
    _loadingIndicatorMaterial?.SetShaderParameter(CooldownParam, Mathf.Clamp(progress, 0f, 1f));
  }
}
