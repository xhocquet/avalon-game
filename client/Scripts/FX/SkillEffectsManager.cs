using System;
using System.Collections.Generic;
using Godot;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Godot;

namespace Meesles.Avalon.Client.Scripts.View;

// Client-only active-skill visuals. Unsupported projectile skills keep their telegraph only.
public partial class SkillEffectsManager : Node {
  private const float ProjectileHeight = 1.5f;

  private static PackedScene _crystalBulletScene;
  private static PackedScene _hairballScene;

  private readonly Dictionary<int, ActiveProjectile> _active = new();
  private readonly SkillEffectCatalog _catalog = new();
  private readonly ChargeCircleEffects _chargeCircles = new();
  private readonly MoltBuffEffects _moltBuffs = new();
  private readonly ChrysalisEffects _chrysalisEffects = new();
  private readonly CrystalGiantEffects _crystalGiantEffects = new();
  private readonly HairballImpactEffects _hairballImpacts = new();
  private readonly StrangleEffects _strangleEffects = new();
  private readonly AllHairWizardEffects _allHairWizardEffects = new();
  private readonly SnailheadEffects _snailheadEffects = new();
  private readonly PickleKnightEffects _pickleKnightEffects = new();
  private readonly SkinwalkerEffects _skinwalkerEffects = new();
  private readonly SnailTrailEffects _snailTrailEffects = new();
  private readonly VenomousSlobberEffects _venomousSlobberEffects = new();
  private readonly Stack<CrystalBulletEffect> _idleCrystalBullets = new();
  private readonly Stack<HairballEffect> _idleHairballs = new();
  private readonly List<int> _staleProjectileIds = new();

  private IKlothoEngine _engine;
  private Node3D _layer;
  private float _secondsSinceTick;

  public void Attach(IKlothoEngine engine, SimEventHub events = null, EntityViewUpdaterNode view = null) {
    Detach();
    _engine = engine;
    _engine.OnTickExecuted += HandleTickExecuted;
    ProcessPriority = 1001;

    _layer = new Node3D { Name = "SkillEffectsLayer" };
    AddChild(_layer);
    _snailTrailEffects.Attach(_layer);
    _chargeCircles.Attach(_layer, view);
    _moltBuffs.Attach(_layer, view);
    _strangleEffects.Attach(_layer, view);
    if (events != null) {
      _chrysalisEffects.Attach(events, _layer);
      _crystalGiantEffects.Attach(events, _layer, engine, view);
      _hairballImpacts.Attach(events, _layer, ProjectileHeight);
      _allHairWizardEffects.Attach(events, _layer, engine, view);
      _snailheadEffects.Attach(events, _layer, engine, view);
      _pickleKnightEffects.Attach(events, _layer, engine, view);
      _skinwalkerEffects.Attach(events, _layer, engine, view);
      _venomousSlobberEffects.Attach(events, _layer, engine);
    }

    var frame = _engine.PredictedFrame.Frame;
    if (frame != null)
      Reconcile(ref frame);
  }

  public void Detach() {
    _strangleEffects.Detach();
    _hairballImpacts.Detach();
    _allHairWizardEffects.Detach();
    _snailheadEffects.Detach();
    _pickleKnightEffects.Detach();
    _skinwalkerEffects.Detach();
    _snailTrailEffects.Detach();
    _chargeCircles.Detach();
    _moltBuffs.Detach();
    _chrysalisEffects.Detach();
    _crystalGiantEffects.Detach();
    _venomousSlobberEffects.Detach();
    if (_engine != null)
      _engine.OnTickExecuted -= HandleTickExecuted;
    _engine = null;

    foreach (var effect in _active.Values)
      Release(effect.Node);
    _active.Clear();
    ClearPools();

    if (_layer != null && GodotObject.IsInstanceValid(_layer))
      _layer.QueueFree();
    _layer = null;
    _secondsSinceTick = 0f;
  }

  public override void _Process(double delta) {
    if (_engine == null) return;

    _secondsSinceTick = Mathf.Min(_secondsSinceTick + (float)delta, _engine.TickInterval / 1000f);
    _snailTrailEffects.ApplyVisual(_secondsSinceTick);
    _chargeCircles.ApplyVisual(_secondsSinceTick);
    _moltBuffs.ApplyVisual(_secondsSinceTick);
    _strangleEffects.ApplyVisual(_secondsSinceTick);
    foreach (var effect in _active.Values) {
      effect.SpinRadians = Mathf.PosMod(effect.SpinRadians + effect.Node.SpinRadiansPerSecond * (float)delta,
        Mathf.Tau);
      ApplyPose(effect, _secondsSinceTick);
    }
  }

  public override void _ExitTree() {
    Detach();
    base._ExitTree();
  }

  private void HandleTickExecuted(int _) {
    _secondsSinceTick = 0f;
    var frame = _engine?.PredictedFrame.Frame;
    if (frame != null)
      Reconcile(ref frame);
  }

  private void Reconcile(ref Frame frame) {
    _snailTrailEffects.Reconcile(ref frame);
    _chargeCircles.Reconcile(ref frame);
    _moltBuffs.Reconcile(ref frame);
    _strangleEffects.Reconcile(ref frame);
    _staleProjectileIds.Clear();
    foreach (var projectileId in _active.Keys)
      _staleProjectileIds.Add(projectileId);

    var filter = frame.Filter<Projectile, TransformComponent>();
    while (filter.Next(out var entity)) {
      ref readonly var projectile = ref frame.GetReadOnly<Projectile>(entity);
      if (_active.TryGetValue(projectile.ProjectileId, out var effect) &&
          effect.SkillAssetId == projectile.SkillAssetId)
        _staleProjectileIds.Remove(projectile.ProjectileId);
    }

    foreach (var projectileId in _staleProjectileIds) {
      Release(_active[projectileId].Node);
      _active.Remove(projectileId);
    }

    var (crystalCount, hairballCount) = PoolTargets(ref frame);
    ResizePools(crystalCount, hairballCount);

    filter = frame.Filter<Projectile, TransformComponent>();
    while (filter.Next(out var entity)) {
      ref readonly var projectile = ref frame.GetReadOnly<Projectile>(entity);
      if (!_catalog.HasProjectileEffect(projectile.SkillAssetId))
        continue;

      ref readonly var transform = ref frame.GetReadOnly<TransformComponent>(entity);
      if (!_active.TryGetValue(projectile.ProjectileId, out var effect)) {
        effect = new ActiveProjectile(RentProjectile(projectile.SkillAssetId), projectile.SkillAssetId);
        _active[projectile.ProjectileId] = effect;
        effect.Configure(projectile.Radius);
      }

      effect.Position = transform.Position;
      effect.Direction = projectile.Direction;
    }

    ResizePools(crystalCount, hairballCount);
  }

  private ProjectileEffect RentProjectile(int skillAssetId) => skillAssetId switch {
    AssetIds.SkillCrystalGiantTertiary => Rent(ref _crystalBulletScene,
      SkillEffectCatalog.CrystalBulletScenePath, _idleCrystalBullets),
    AssetIds.SkillHairyWizardPrimary => Rent(ref _hairballScene,
      SkillEffectCatalog.HairballScenePath, _idleHairballs),
    _ => throw new InvalidOperationException($"[SkillEffects] Unsupported skill {skillAssetId}.")
  };

  private T Rent<T>(ref PackedScene scene, string scenePath, Stack<T> pool) where T : ProjectileEffect {
    scene ??= GD.Load<PackedScene>(scenePath);
    var bullet = pool.Count > 0 ? pool.Pop() : scene?.Instantiate<T>();
    if (bullet == null)
      throw new InvalidOperationException($"[SkillEffects] Failed to instantiate {scenePath}.");

    _layer.AddChild(bullet);
    return bullet;
  }

  private void Release(ProjectileEffect bullet) {
    if (bullet == null || !GodotObject.IsInstanceValid(bullet)) return;
    bullet.ResetVisual();
    bullet.GetParent()?.RemoveChild(bullet);
    switch (bullet) {
      case CrystalBulletEffect crystal:
        _idleCrystalBullets.Push(crystal);
        break;
      case HairballEffect hairball:
        _idleHairballs.Push(hairball);
        break;
    }
  }

  private static (int Crystals, int Hairballs) PoolTargets(ref Frame frame) {
    var crystals = 0;
    var hairballs = 0;
    var heroes = frame.Filter<Hero, Skills>();
    while (heroes.Next(out var entity)) {
      ref readonly var skills = ref frame.GetReadOnly<Skills>(entity);
      for (var slot = 0; slot < Skills.MaxSlots; slot++) {
        var skillAssetId = skills.GetSkillAssetId(slot);
        if (skillAssetId != AssetIds.SkillCrystalGiantTertiary &&
            skillAssetId != AssetIds.SkillHairyWizardPrimary)
          continue;
        if (!frame.AssetRegistry.TryGet<SkillAsset>(skillAssetId, out var skill))
          continue;
        var count = skill.ProjectileCount > 0 ? skill.ProjectileCount : 0; // One volley per caster; overflow rents on demand.
        if (skillAssetId == AssetIds.SkillCrystalGiantTertiary)
          crystals += count;
        else
          hairballs += count;
      }
    }
    return (crystals, hairballs);
  }

  private void ResizePools(int crystals, int hairballs) {
    foreach (var effect in _active.Values) {
      if (effect.SkillAssetId == AssetIds.SkillCrystalGiantTertiary)
        crystals--;
      else if (effect.SkillAssetId == AssetIds.SkillHairyWizardPrimary)
        hairballs--;
    }
    ResizePool(ref _crystalBulletScene, SkillEffectCatalog.CrystalBulletScenePath, _idleCrystalBullets,
      Math.Max(crystals, 0));
    ResizePool(ref _hairballScene, SkillEffectCatalog.HairballScenePath, _idleHairballs,
      Math.Max(hairballs, 0));
  }

  private void ClearPools() {
    while (_idleCrystalBullets.Count > 0)
      _idleCrystalBullets.Pop().Free();
    while (_idleHairballs.Count > 0)
      _idleHairballs.Pop().Free();
  }

  private static void ResizePool<T>(ref PackedScene scene, string scenePath, Stack<T> pool, int idleCount)
    where T : ProjectileEffect {
    while (pool.Count > idleCount)
      pool.Pop().Free();
    if (pool.Count >= idleCount) return;

    scene ??= GD.Load<PackedScene>(scenePath);
    if (scene == null) {
      GD.PushError($"[SkillEffects] Missing {scenePath}.");
      return;
    }

    while (pool.Count < idleCount) {
      var bullet = scene.Instantiate<T>();
      if (bullet == null) break;
      pool.Push(bullet);
    }
  }

  private static void ApplyPose(ActiveProjectile effect, float secondsSinceTick) {
    var direction = effect.Direction.ToVector3();
    var position = effect.Position.ToVector3() + direction * secondsSinceTick;
    position.Y += ProjectileHeight;
    effect.Node.GlobalPosition = position;
    if (direction.LengthSquared() <= 0.0001f) return;

    effect.Node.LookAt(position + direction, Vector3.Up);
    effect.Node.RotateObjectLocal(Vector3.Up, Mathf.Pi); // The generated crystal points along +Z.
    effect.Node.RotateObjectLocal(effect.Node.SpinAxis, effect.SpinRadians);
  }

  private sealed class ActiveProjectile(ProjectileEffect node, int skillAssetId) {
    public readonly ProjectileEffect Node = node;
    public readonly int SkillAssetId = skillAssetId;
    public FPVector3 Direction;
    public FPVector3 Position;
    public float SpinRadians;

    public void Configure(FP64 radius) => Node.Configure(radius.ToFloat());
  }
}
