using System;
using System.Collections.Generic;
using Godot;
using Meesles.Avalon.Client.Scripts.Interfaces;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Godot;

namespace Meesles.Avalon.Client.Scripts.View;

public class VfxManager {
  private const string ExplosionScenePath = "res://Scenes/FX/BotwExplosionMega.tscn";
  private const string TurretScenePath = Scenes.Turret;
  private const string ContainerScenePath = Scenes.Container;

  // How long the red death silhouette lingers before it has fully faded out.
  private const float DeathTintFadeSeconds = 0.7f;
  private static readonly Color DeathTintColor = new(0.9f, 0.06f, 0.06f);

  // Non-body child nodes that ship inside the unit prefab; tinting/showing them as part of the
  // corpse would look wrong, so they're hidden on the spawned silhouette.
  private static readonly string[] NonBodyNodes = { "LoadingIndicator", "SelectionIndicator" };

  private static PackedScene _explosionScene;
  private static PackedScene _turretScene;
  private static PackedScene _containerScene;

  private readonly SkillCatalog _skills = SkillCatalog.CreateDefault();

  private IDisposable _attackCanceledSub;
  private IDisposable _attackHitSub;
  private IDisposable _attackProcSub;
  private IDisposable _attackWindupSub;
  private IDisposable _turretDestroyedSub;
  private IDisposable _crystalDestroyedSub;
  private EntityViewUpdaterNode _view;
  private Mesh _attackLineMesh;
  private IKlothoEngine _engine;
  private IDisposable _rewardSub;
  private PackedScene _hairballScene;
  private readonly Dictionary<int, AutoAttackHairball> _hairballs = new();

  public void Attach(SimEventHub events, EntityViewUpdaterNode view, IKlothoEngine engine) {
    Detach();
    _view = view;
    _engine = engine;
    _rewardSub = events.OnConfirmed<RewardGainedEvent>(HandleReward);
    _attackLineMesh = DebugAttackLine.CreateMesh();
    // OnFx, not OnPredicted: a local host verifies every tick, so nothing arrives on the predicted
    // stream there and VFX subscribed to it play only in networked matches. A mispredicted flash is
    // transient and harmless, which is why UI state uses confirmed events but VFX do not.
    _attackWindupSub = events.OnFx<AttackWindupStartedEvent>(HandleAttackWindupStarted);
    _attackCanceledSub = events.OnFx<AttackWindupCanceledEvent>(HandleAttackWindupCanceled);
    _attackHitSub = events.OnFx<AttackHitEvent>(HandleAttackHit);
    _attackProcSub = events.OnFx<AttackProcConsumedEvent>(HandleAttackProcConsumed);
    // Death effects use the confirmed stream: these events are Synced, and a big one-shot
    // explosion would be jarring to spawn on a mispredicted tick and then rewind.
    _turretDestroyedSub = events.OnConfirmed<TurretDestroyedEvent>(HandleTurretDestroyed);
    _crystalDestroyedSub = events.OnConfirmed<CrystalDestroyedEvent>(HandleCrystalDestroyed);
  }

  public void Detach() {
    _rewardSub?.Dispose();
    _rewardSub = null;
    foreach (var ball in _hairballs.Values)
      if (GodotObject.IsInstanceValid(ball)) ball.QueueFree();
    _hairballs.Clear();
    _engine = null;
    _attackWindupSub?.Dispose();
    _attackWindupSub = null;
    _attackCanceledSub?.Dispose();
    _attackCanceledSub = null;
    _attackHitSub?.Dispose();
    _attackHitSub = null;
    _attackProcSub?.Dispose();
    _attackProcSub = null;
    _turretDestroyedSub?.Dispose();
    _turretDestroyedSub = null;
    _crystalDestroyedSub?.Dispose();
    _crystalDestroyedSub = null;
    _view = null;
    _attackLineMesh = null;
  }

  // The swing, not the hit: an attack clip started when damage lands plays its wind-up after the
  // blow it is supposed to lead. The sim raises this AttackWindup seconds ahead of the hit and hands
  // over the length, so the view can put the clip's contact frame on the damage tick.
  private void HandleAttackWindupStarted(AttackWindupStartedEvent evt) {
    if (_view == null) return;

    var views = _view.ViewsByUnitId;
    views.TryGetValue(evt.AttackerUnitId, out var attackerView);
    views.TryGetValue(evt.TargetUnitId, out var targetView);

    var attackerPos = attackerView?.GlobalPosition ?? evt.AttackerPosition.ToVector3();
    var targetPos = targetView?.GlobalPosition ?? evt.TargetPosition.ToVector3();
    var windupSeconds = evt.WindupSeconds.ToFloat();
    var frame = _engine?.PredictedFrame.Frame;
    if (attackerView != null && frame != null && frame.Has<Hero>(attackerView.EntityRef) &&
        frame.GetReadOnly<Hero>(attackerView.EntityRef).HeroStatsAssetId == AssetIds.HeroHairyWizard) {
      ClearHairball(evt.AttackerUnitId);
      _hairballScene ??= GD.Load<PackedScene>(SkillEffectCatalog.HairballScenePath);
      var ball = new AutoAttackHairball();
      _view.AddChild(ball);
      ball.Start(_hairballScene, attackerPos, targetPos, targetView, windupSeconds);
      _hairballs[evt.AttackerUnitId] = ball;
    }

    // The debug line stands in for rigs that have no attack clip yet.
    if (attackerView is not IAttackableView attacker ||
        !attacker.OnAttackWindupVfx(targetPos, windupSeconds))
      _view.AddChild(DebugAttackLine.Create(attackerPos, targetPos, _attackLineMesh));
  }

  private void HandleAttackWindupCanceled(AttackWindupCanceledEvent evt) {
    ClearHairball(evt.AttackerUnitId);
    if (_view == null) return;

    if (_view.ViewsByUnitId.TryGetValue(evt.AttackerUnitId, out var attackerView) &&
        attackerView is IAttackableView attacker)
      attacker.OnAttackCanceledVfx();
  }

  private void ClearHairball(int attackerId) {
    if (_hairballs.Remove(attackerId, out var ball) && GodotObject.IsInstanceValid(ball)) ball.QueueFree();
  }

  private void HandleReward(RewardGainedEvent evt) {
    var frame = _engine?.VerifiedFrame.Frame ?? _engine?.PredictedFrame.Frame;
    if (_view == null || frame == null ||
        !UnitLookup.TryGetPlayerTeamId(ref frame, _engine.LocalPlayerId, out var teamId) || evt.TeamId != teamId) return;
    var position = evt.Position.ToVector3();
    if (evt.Gold > 0)
      _view.AddChild(DamageNumber.CreateReward($"+{evt.Gold}g", position, new Color(1, 0.85f, 0.2f)));
    if (evt.Xp > 0) {
      var xp = DamageNumber.CreateReward($"+{evt.Xp} XP", position, Colors.White, compact: true);
      if (evt.Gold > 0) xp.Offset = new Vector2(0, -76);
      _view.AddChild(xp);
    }
    if (evt.ResourceAmount > 0)
      _view.AddChild(DamageNumber.CreateReward($"+{evt.ResourceAmount} " +
        (evt.ResourceTypeId == AssetIds.PickupTypeWater ? "Water" : "Resource"), position, new Color(0.35f, 0.85f, 1)));
  }

  private void HandleAttackHit(AttackHitEvent evt) {
    if (_view == null) return;

    var views = _view.ViewsByUnitId;
    views.TryGetValue(evt.AttackerUnitId, out var attackerView);
    views.TryGetValue(evt.TargetUnitId, out var targetView);

    var attackerPos = attackerView?.GlobalPosition ?? evt.AttackerPosition.ToVector3();
    var targetPos = targetView?.GlobalPosition ?? evt.TargetPosition.ToVector3();

    var frame = _engine?.PredictedFrame.Frame ?? _engine?.VerifiedFrame.Frame;
    if (frame != null && UnitLookup.TryGetPlayerHero(ref frame, _engine.LocalPlayerId, out var hero) &&
        UnitLookup.GetUnitId(ref frame, hero) == evt.AttackerUnitId)
      _view.AddChild(DamageNumber.Create(evt.Damage.ToFloat(), targetPos, evt.IsCrit != 0));

    if (targetView is IAttackableView target)
      target.OnHitVfx(evt.Damage.ToFloat(), attackerPos);
  }

  // Each effect an attack spends names itself, so this scales to a hit that consumed several: one
  // popup each, rather than one flag on the hit that cannot say which of them landed. Placeholder
  // until skills carry their own VFX, keyed off SkillAssetId.
  private void HandleAttackProcConsumed(AttackProcConsumedEvent evt) {
    if (_view == null) return;

    var label = _skills.TryResolve(evt.SkillAssetId, out var skill) ? skill.Name : "Proc";
    var position = _view.ViewsByUnitId.TryGetValue(evt.TargetUnitId, out var targetView)
      ? targetView.GlobalPosition
      : Vector3.Zero;

    _view.AddChild(DamageNumber.Create(label, position, emphasized: true));
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
    SpawnDeathTint(ref _containerScene, ContainerScenePath, position);
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
