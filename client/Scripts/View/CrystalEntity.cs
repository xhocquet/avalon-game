using Godot;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Core;

namespace Meesles.Avalon.Client.Scripts.View;

public partial class CrystalEntity : TeamEntityViewNode, IAttackableView, INamedView {
  private const string UnitsGroup = "units";
  public string DisplayName => "Crystal";

  [Export] public float SelectPickRadius { get; set; } = -1.0f;
  [Export] public float SelectPickHeight { get; set; } = -1.0f;

  public void OnAttackVfx(Vector3 targetPosition) { }

  public void OnHitVfx(int damage, Vector3 attackerPosition) { }

  public override void OnInitialize() {
    EntityViewPhysics.DisableGodotCollision(this);
    EntityViewPhysics.AddSelectionCollider(this, SelectPickRadius, SelectPickHeight);
  }

  public override void OnActivate(FrameRef frame) {
    AddToGroup(UnitsGroup);

    var live = frame.Frame;
    if (live != null && live.Has<Unit>(EntityRef))
      SetCachedUnitId(live.GetReadOnly<Unit>(EntityRef).UnitId);
    BindTeam(frame);
  }

  public override void OnDeactivate() {
    RemoveFromGroup(UnitsGroup);
    ClearTeam();
  }
}
