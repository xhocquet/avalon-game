using System;
using Godot;
using Meesles.Avalon.Sim;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Godot;

namespace Meesles.Avalon.Client.Scripts.View;

public class VfxManager {
  private const string ExplosionScenePath = "res://Scenes/FX/BotwExplosionMega.tscn";
  private const string TurretScenePath = "res://Scenes/Objects/Turret.tscn";
  private const string CrystalScenePath = "res://Scenes/Objects/Crystal.tscn";

  // How long the red death silhouette lingers before it has fully faded out.
  private const float DeathTintFadeSeconds = 0.7f;
  private static readonly Color DeathTintColor = new(0.9f, 0.06f, 0.06f);

  // Non-body child nodes that ship inside the unit prefab; tinting/showing them as part of the
  // corpse would look wrong, so they're hidden on the spawned silhouette.
  private static readonly string[] NonBodyNodes = { "LoadingIndicator", "SelectionIndicator" };

  private static PackedScene _explosionScene;
  private static PackedScene _turretScene;
  private static PackedScene _crystalScene;

  private IDisposable _attackHitSub;
  private IDisposable _turretDestroyedSub;
  private IDisposable _crystalDestroyedSub;
  private EntityViewUpdaterNode _view;

  public void Attach(SimEventHub events, EntityViewUpdaterNode view) {
    Detach();
    _view = view;
    // Predicted (not confirmed) so hits flash immediately on the local tick; a mispredicted
    // flash is transient and harmless, which is why UI state uses confirmed events but VFX do not.
    _attackHitSub = events.OnPredicted<AttackHitEvent>(HandleAttackHit);
    // Death effects use the confirmed stream: these events are Synced, and a big one-shot
    // explosion would be jarring to spawn on a mispredicted tick and then rewind.
    _turretDestroyedSub = events.OnConfirmed<TurretDestroyedEvent>(HandleTurretDestroyed);
    _crystalDestroyedSub = events.OnConfirmed<CrystalDestroyedEvent>(HandleCrystalDestroyed);
  }

  public void Detach() {
    _attackHitSub?.Dispose();
    _attackHitSub = null;
    _turretDestroyedSub?.Dispose();
    _turretDestroyedSub = null;
    _crystalDestroyedSub?.Dispose();
    _crystalDestroyedSub = null;
    _view = null;
  }

  private void HandleAttackHit(AttackHitEvent evt) {
    if (_view == null) return;

    var views = _view.ViewsByUnitId;
    views.TryGetValue(evt.AttackerUnitId, out var attackerView);
    views.TryGetValue(evt.TargetUnitId, out var targetView);

    var attackerPos = attackerView?.GlobalPosition ?? evt.AttackerPosition.ToVector3();
    var targetPos = targetView?.GlobalPosition ?? evt.TargetPosition.ToVector3();

    var line = DebugAttackLine.Create(attackerPos, targetPos);
    _view.AddChild(line);

    if (attackerView is IAttackableView attacker)
      attacker.OnAttackVfx(targetPos);
    if (targetView is IAttackableView target)
      target.OnHitVfx(evt.Damage, attackerPos);
  }

  private void HandleTurretDestroyed(TurretDestroyedEvent evt) {
    if (_view == null) return;
    var position = ResolveDeathPosition(evt.UnitId, evt.Position);
    SpawnDeathExplosion(position);
    SpawnDeathTint(ref _turretScene, TurretScenePath, position);
  }

  private void HandleCrystalDestroyed(CrystalDestroyedEvent evt) {
    if (_view == null) return;
    var position = ResolveDeathPosition(evt.UnitId, evt.Position);
    SpawnDeathExplosion(position);
    SpawnDeathTint(ref _crystalScene, CrystalScenePath, position);
  }

  // Prefer the live view position when it's still around, but the entity is destroyed the same
  // tick it dies, so its pooled view is usually already gone — fall back to the sim position the
  // event carries.
  private Vector3 ResolveDeathPosition(int unitId, FPVector3 simPosition) {
    return _view.ViewsByUnitId.TryGetValue(unitId, out var view)
      ? view.GlobalPosition
      : simPosition.ToVector3();
  }

  private void SpawnDeathExplosion(Vector3 position) {
    _explosionScene ??= GD.Load<PackedScene>(ExplosionScenePath);
    if (_explosionScene?.Instantiate() is not GpuParticles3D explosion) return;

    // OneShot + the Finished signal lets the burst play through its lifetime and then clean itself
    // up, instead of the authored scene looping the explosion forever.
    explosion.OneShot = true;
    explosion.Emitting = true;
    explosion.Finished += explosion.QueueFree;

    _view.AddChild(explosion);
    explosion.GlobalPosition = position;
  }

  // Placeholder death effect (until real death animations exist): drop a copy of the unit's own
  // prefab at the death spot, flatten it to solid red, and fade it out. The live view has already
  // been recycled by the pool by the time the (Synced) death event arrives, so we rebuild the
  // silhouette from the prefab rather than reusing the recycled node.
  private void SpawnDeathTint(ref PackedScene cachedScene, string scenePath, Vector3 position) {
    cachedScene ??= GD.Load<PackedScene>(scenePath);
    if (cachedScene?.Instantiate() is not Node3D corpse) return;

    var tint = new StandardMaterial3D {
      ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
      Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
      AlbedoColor = DeathTintColor,
    };
    ApplyDeathTint(corpse, tint);

    _view.AddChild(corpse);
    corpse.GlobalPosition = position;

    // One tween drives the shared material's alpha, so the whole silhouette fades together, then
    // the corpse frees itself.
    var tween = corpse.CreateTween();
    tween.TweenProperty(tint, "albedo_color:a", 0f, DeathTintFadeSeconds);
    tween.TweenCallback(Callable.From(corpse.QueueFree));
  }

  private static void ApplyDeathTint(Node node, Material tint) {
    if (Array.IndexOf(NonBodyNodes, node.Name.ToString()) >= 0) {
      if (node is Node3D hidden) hidden.Visible = false;
      return; // Don't recurse into or tint non-body decorations.
    }

    if (node is MeshInstance3D mesh)
      mesh.MaterialOverride = tint;

    foreach (var child in node.GetChildren())
      ApplyDeathTint(child, tint);
  }
}
