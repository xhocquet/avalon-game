using Godot;
using Meesles.Avalon.Client.Scripts.Interfaces;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Core;

namespace Meesles.Avalon.Client.Scripts.Entities;

public partial class CrystalEntity : TeamEntityViewNode, IAttackableView, INamedView {
  private const string UnitsGroup = "units";
  public virtual string DisplayName => "Crystal";
  protected override Texture2D MinimapIcon => GD.Load<Texture2D>("res://Assets/Portraits/Minimap/Base.webp");
  protected override float MinimapPixelSize => 0.15f;

  [Export] public float SelectPickRadius { get; set; } = -1.0f;
  [Export] public float SelectPickHeight { get; set; } = -1.0f;

  public bool OnAttackWindupVfx(Vector3 targetPosition, float windupSeconds) => false;

  public void OnAttackCanceledVfx() { }

  public void OnHitVfx(float damage, Vector3 attackerPosition) { }

  public override void OnInitialize() {
    EntityViewPhysics.DisableGodotCollision(this);
    EntityViewPhysics.AddSelectionCollider(this, SelectPickRadius, SelectPickHeight);
  }

  public override void OnActivate(FrameRef frame) {
    AddToGroup(UnitsGroup);

    var live = frame.Frame;
    if (live != null && live.Has<UnitIdentity>(EntityRef))
      SetCachedUnitId(live.GetReadOnly<UnitIdentity>(EntityRef).UnitId);
    BindTeam(frame);
  }

  public override void OnDeactivate() {
    RemoveFromGroup(UnitsGroup);
    ClearTeam();
  }
}
