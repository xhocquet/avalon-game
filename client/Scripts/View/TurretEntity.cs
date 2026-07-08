using global::Godot;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Godot;
using Meesles.Avalon.Sim.Models;

namespace Meesles.Avalon {
  public partial class TurretEntity : EntityViewNode, ISelectableTeamView, IAttackableView {
    private const string UnitsGroup = "units";

    private int _teamId = -1;

    public override void OnInitialize() {
      EntityViewPhysics.DisableGodotCollision(this);
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

    public bool TeamMatches(int teamId) => _teamId == teamId;

    public void OnAttackVfx(Vector3 targetPosition) {
      // TODO: turret fire animation / particles
    }

    public void OnHitVfx(int damage, Vector3 attackerPosition) {
      // TODO: hit reaction / particles
    }
  }
}
