using System;
using System.Collections.Generic;
using Godot;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Client.Scripts.View;

// Client-only active-skill visuals. Unsupported projectile skills keep their telegraph only.
public partial class SkillEffectsManager : Node {
  private const int PrewarmCount = 64;
  private const float CrystalBulletHeight = 1.5f;
  private const float CrystalBulletSpinRadiansPerSecond = Mathf.Tau * 3f;

  private static PackedScene _crystalBulletScene;

  private readonly Dictionary<int, ActiveProjectile> _active = new();
  private readonly SkillEffectCatalog _catalog = new();
  private readonly Stack<CrystalBulletEffect> _idleCrystalBullets = new();
  private readonly List<int> _staleProjectileIds = new();

  private IKlothoEngine _engine;
  private Node3D _layer;
  private float _secondsSinceTick;

  public void Attach(IKlothoEngine engine) {
    Detach();
    _engine = engine;
    _engine.OnTickExecuted += HandleTickExecuted;
    ProcessPriority = 1001;

    _layer = new Node3D { Name = "SkillEffectsLayer" };
    AddChild(_layer);
    Prewarm();

    var frame = _engine.PredictedFrame.Frame;
    if (frame != null)
      Reconcile(ref frame);
  }

  public void Detach() {
    if (_engine != null)
      _engine.OnTickExecuted -= HandleTickExecuted;
    _engine = null;

    foreach (var effect in _active.Values)
      Release(effect.Node);
    _active.Clear();

    if (_layer != null && GodotObject.IsInstanceValid(_layer))
      _layer.QueueFree();
    _layer = null;
    _secondsSinceTick = 0f;
  }

  public override void _Process(double delta) {
    if (_active.Count == 0 || _engine == null) return;

    _secondsSinceTick = Mathf.Min(_secondsSinceTick + (float)delta, _engine.TickInterval / 1000f);
    foreach (var effect in _active.Values) {
      effect.SpinRadians = Mathf.PosMod(effect.SpinRadians + CrystalBulletSpinRadiansPerSecond * (float)delta,
        Mathf.Tau);
      ApplyPose(effect, _secondsSinceTick);
    }
  }

  public override void _ExitTree() {
    Detach();
    while (_idleCrystalBullets.Count > 0)
      _idleCrystalBullets.Pop().QueueFree();
    base._ExitTree();
  }

  private void HandleTickExecuted(int _) {
    _secondsSinceTick = 0f;
    var frame = _engine?.PredictedFrame.Frame;
    if (frame != null)
      Reconcile(ref frame);
  }

  private void Reconcile(ref Frame frame) {
    _staleProjectileIds.Clear();
    foreach (var projectileId in _active.Keys)
      _staleProjectileIds.Add(projectileId);

    var filter = frame.Filter<Projectile, TransformComponent>();
    while (filter.Next(out var entity)) {
      ref readonly var projectile = ref frame.GetReadOnly<Projectile>(entity);
      if (!_catalog.HasProjectileEffect(projectile.SkillAssetId))
        continue;

      _staleProjectileIds.Remove(projectile.ProjectileId);
      ref readonly var transform = ref frame.GetReadOnly<TransformComponent>(entity);
      if (!_active.TryGetValue(projectile.ProjectileId, out var effect)) {
        effect = new ActiveProjectile(RentCrystalBullet());
        _active.Add(projectile.ProjectileId, effect);
        effect.Configure(projectile.Radius);
      }

      effect.Position = transform.Position;
      effect.Direction = projectile.Direction;
    }

    foreach (var projectileId in _staleProjectileIds) {
      Release(_active[projectileId].Node);
      _active.Remove(projectileId);
    }
  }

  private CrystalBulletEffect RentCrystalBullet() {
    _crystalBulletScene ??= GD.Load<PackedScene>(SkillEffectCatalog.CrystalBulletScenePath);
    var bullet = _idleCrystalBullets.Count > 0 ? _idleCrystalBullets.Pop() : _crystalBulletScene?.Instantiate<CrystalBulletEffect>();
    if (bullet == null)
      throw new InvalidOperationException($"[SkillEffects] Failed to instantiate {SkillEffectCatalog.CrystalBulletScenePath}.");

    _layer.AddChild(bullet);
    return bullet;
  }

  private void Release(CrystalBulletEffect bullet) {
    if (bullet == null || !GodotObject.IsInstanceValid(bullet)) return;
    bullet.ResetVisual();
    bullet.GetParent()?.RemoveChild(bullet);
    _idleCrystalBullets.Push(bullet);
  }

  private void Prewarm() {
    _crystalBulletScene ??= GD.Load<PackedScene>(SkillEffectCatalog.CrystalBulletScenePath);
    if (_crystalBulletScene == null) {
      GD.PushError($"[SkillEffects] Missing {SkillEffectCatalog.CrystalBulletScenePath}.");
      return;
    }

    while (_idleCrystalBullets.Count < PrewarmCount) {
      var bullet = _crystalBulletScene.Instantiate<CrystalBulletEffect>();
      if (bullet == null) break;
      _idleCrystalBullets.Push(bullet);
    }
  }

  private static void ApplyPose(ActiveProjectile effect, float secondsSinceTick) {
    var direction = effect.Direction.ToVector3();
    var position = effect.Position.ToVector3() + direction * secondsSinceTick;
    position.Y += CrystalBulletHeight;
    effect.Node.GlobalPosition = position;
    if (direction.LengthSquared() <= 0.0001f) return;

    effect.Node.LookAt(position + direction, Vector3.Up);
    effect.Node.RotateObjectLocal(Vector3.Up, Mathf.Pi); // The generated crystal points along +Z.
    effect.Node.RotateObjectLocal(Vector3.Back, effect.SpinRadians);
  }

  private sealed class ActiveProjectile(CrystalBulletEffect node) {
    public readonly CrystalBulletEffect Node = node;
    public FPVector3 Direction;
    public FPVector3 Position;
    public float SpinRadians;

    public void Configure(FP64 radius) => Node.Configure(radius.ToFloat());
  }
}
