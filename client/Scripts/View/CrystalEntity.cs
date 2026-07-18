using Godot;
using Meesles.Avalon.Sim.Models;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Godot;

namespace Meesles.Avalon;

public partial class CrystalEntity : EntityViewNode, ISelectableTeamView, IAttackableView {
  private const string UnitsGroup = "units";

  // Selection hitbox in world metres. Leave <= 0 to auto-derive from mesh bounds.
  [Export] public float SelectPickRadius { get; set; } = 1.0f;
  [Export] public float SelectPickHeight { get; set; } = 2.0f;

  private int _teamId = -1;

  public void OnAttackVfx(Vector3 targetPosition) { }

  public void OnHitVfx(int damage, Vector3 attackerPosition) {
    // TODO: hit reaction / particles
  }

  public bool TeamMatches(int teamId) {
    return _teamId == teamId;
  }

  public override void OnInitialize() {
    EntityViewPhysics.DisableGodotCollision(this);
    EntityViewPhysics.AddSelectionCollider(this, SelectPickRadius, SelectPickHeight);
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
}
