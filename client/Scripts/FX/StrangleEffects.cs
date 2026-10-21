using System.Collections.Generic;
using Godot;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Godot;

namespace Meesles.Avalon.Client.Scripts.View;

public sealed class StrangleEffects {
  private const float Height = 1.5f;
  private const float BaseRadius = 0.25f;
  private static PackedScene _scene;
  private readonly Dictionary<(bool Attached, long Id), ActiveLock> _active = new();
  private readonly Stack<Node3D> _pool = new();
  private readonly List<(bool Attached, long Id)> _stale = new();
  private readonly UnitLookup.Index _units = new();
  private Node3D _layer;
  private EntityViewUpdaterNode _view;

  public void Attach(Node3D layer, EntityViewUpdaterNode view) {
    Detach();
    _layer = layer;
    _view = view;
  }

  public void Detach() {
    foreach (var effect in _active.Values) effect.Node.QueueFree();
    _active.Clear();
    while (_pool.Count > 0) _pool.Pop().QueueFree();
    _stale.Clear();
    _layer = null;
    _view = null;
  }

  public void Reconcile(ref Frame frame) {
    if (_layer == null) return;
    _units.Rebuild(ref frame);
    _stale.Clear();
    foreach (var key in _active.Keys) _stale.Add(key);

    var projectiles = frame.Filter<Projectile, TransformComponent>();
    while (projectiles.Next(out var entity)) {
      ref readonly var projectile = ref frame.GetReadOnly<Projectile>(entity);
      if (projectile.SkillAssetId != AssetIds.SkillHairyWizardSecondary ||
          !TryGetSource(ref frame, projectile.SourceUnitId, out var source)) continue;

      var effect = Sync(ref frame, (false, projectile.ProjectileId), source, projectile.SourceUnitId,
        frame.GetReadOnly<TransformComponent>(entity).Position, projectile.Radius);
      effect.Direction = projectile.Direction;
      effect.Speed = projectile.Speed;
      effect.RemainingDistance = projectile.RemainingDistance;
      effect.TargetView = null;
    }

    var burns = frame.Filter<DamageOverTime>();
    while (burns.Next(out var effectEntity)) {
      ref readonly var burn = ref frame.GetReadOnly<DamageOverTime>(effectEntity);
      if (burn.SourceSkillAssetId != AssetIds.SkillHairyWizardSecondary || burn.ExpiryTick <= frame.Tick ||
          !TryGetSource(ref frame, burn.SourceUnitId, out var source) ||
          !TryGetSource(ref frame, burn.TargetUnitId, out var entity) ||
          !frame.AssetRegistry.TryGet<SkillAsset>(burn.SourceSkillAssetId, out var skill)) continue;

      var unitId = UnitLookup.GetUnitId(ref frame, entity);
      if (unitId == 0) continue;
      var effect = Sync(ref frame, (true, effectEntity.ToId()), source, burn.SourceUnitId,
        frame.GetReadOnly<TransformComponent>(entity).Position, skill.ProjectileRadius);
      effect.Speed = FP64.Zero;
      effect.RemainingDistance = FP64.Zero;
      effect.TargetView = null;
      _view?.ViewsByUnitId.TryGetValue(unitId, out effect.TargetView);
    }

    foreach (var key in _stale) {
      var effect = _active[key];
      effect.Node.Hide();
      _pool.Push(effect.Node);
      _active.Remove(key);
    }
    ConfigureVisuals();
  }

  private void ConfigureVisuals() {
    foreach (var effect in _active.Values)
      effect.Node.Set("thickness", Mathf.Max(effect.Radius.ToFloat() / BaseRadius, 0.05f));
    ApplyVisual(0f);
  }

  public void ApplyVisual(float secondsSinceTick) {
    foreach (var effect in _active.Values) {
      var root = GodotObject.IsInstanceValid(effect.SourceView)
        ? effect.SourceView.GlobalPosition : effect.SourcePosition.ToVector3();
      var tip = GodotObject.IsInstanceValid(effect.TargetView)
        ? effect.TargetView.GlobalPosition
        : effect.TipPosition.ToVector3() + effect.Direction.ToVector3() *
          Mathf.Min(effect.Speed.ToFloat() * secondsSinceTick, effect.RemainingDistance.ToFloat());
      effect.Node.Call("configure_between", root + Vector3.Up * Height, tip + Vector3.Up * Height);
    }
  }

  private ActiveLock Sync(ref Frame frame, (bool Attached, long Id) key, EntityRef source, int sourceId,
    FPVector3 tip, FP64 radius) {
    _stale.Remove(key);
    if (!_active.TryGetValue(key, out var effect)) {
      effect = new ActiveLock(_pool.Count > 0 ? _pool.Pop() : Create());
      _active.Add(key, effect);
    }
    effect.SourcePosition = frame.GetReadOnly<TransformComponent>(source).Position;
    effect.TipPosition = tip;
    effect.SourceView = null;
    _view?.ViewsByUnitId.TryGetValue(sourceId, out effect.SourceView);
    effect.Radius = radius;
    return effect;
  }

  private bool TryGetSource(ref Frame frame, int sourceId, out EntityRef source) {
    return _units.TryGet(sourceId, out source) && frame.Has<TransformComponent>(source);
  }

  private Node3D Create() {
    _scene ??= GD.Load<PackedScene>(SkillEffectCatalog.AllHairWizardStrangleScenePath);
    var node = _scene.Instantiate<Node3D>();
    _layer.AddChild(node);
    node.Hide();
    return node;
  }

  private sealed class ActiveLock(Node3D node) {
    public readonly Node3D Node = node;
    public FPVector3 SourcePosition;
    public FPVector3 TipPosition;
    public FPVector3 Direction;
    public FP64 Speed;
    public FP64 RemainingDistance;
    public FP64 Radius;
    public EntityViewNode SourceView;
    public EntityViewNode TargetView;
  }
}
