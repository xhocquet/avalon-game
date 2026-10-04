using System;
using System.Collections.Generic;
using Godot;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Assets;
using xpTURN.Klotho.Deterministic.Math;

namespace Meesles.Avalon.Client.Scripts.View;

public sealed class HairballImpactEffects {
  private const string ScenePath = "res://Scenes/FX/Skills/hairball_impact.tscn";
  private const float BaseRadius = 0.4f;
  private static PackedScene _scene;

  private readonly Dictionary<int, (int Tick, long Hash, FP64 Radius)> _projectiles = new(); // Retained until despawn confirmation.
  private readonly Dictionary<(int Tick, int Id, long Hash), HairballImpactEffect> _impacts = new();
  private IDisposable _spawnedSub;
  private IDisposable _despawnedSub;
  private IDisposable _confirmedSub;
  private IDisposable _spawnCanceledSub;
  private IDisposable _impactCanceledSub;
  private Node3D _layer;
  private float _height;

  public void Attach(SimEventHub events, Node3D layer, float height) {
    Detach();
    _layer = layer;
    _height = height;
    _spawnedSub = events.OnFx<SkillProjectileSpawnedEvent>(HandleSpawned);
    _despawnedSub = events.OnFx<SkillProjectileDespawnedEvent>(HandleDespawned);
    _confirmedSub = events.OnConfirmed<SkillProjectileDespawnedEvent>(HandleConfirmed);
    _spawnCanceledSub = events.OnCanceled<SkillProjectileSpawnedEvent>(HandleSpawnCanceled);
    _impactCanceledSub = events.OnCanceled<SkillProjectileDespawnedEvent>(HandleImpactCanceled);
  }

  public void Detach() {
    _spawnedSub?.Dispose();
    _despawnedSub?.Dispose();
    _confirmedSub?.Dispose();
    _spawnCanceledSub?.Dispose();
    _impactCanceledSub?.Dispose();
    _spawnedSub = _despawnedSub = _confirmedSub = _spawnCanceledSub = _impactCanceledSub = null;
    foreach (var impact in _impacts.Values)
      if (GodotObject.IsInstanceValid(impact)) impact.QueueFree();
    _impacts.Clear();
    _projectiles.Clear();
    _layer = null;
  }

  private void HandleSpawned(SkillProjectileSpawnedEvent evt) {
    if (evt.SkillAssetId == AssetIds.SkillHairyWizardPrimary)
      _projectiles[evt.ProjectileId] = (evt.Tick, evt.GetContentHash(), evt.Radius);
    else
      _projectiles.Remove(evt.ProjectileId);
  }

  private void HandleDespawned(SkillProjectileDespawnedEvent evt) {
    if (evt.Reason != (int)SkillProjectileEnd.Hit ||
        !_projectiles.TryGetValue(evt.ProjectileId, out var projectile) || _layer == null)
      return;

    _scene ??= GD.Load<PackedScene>(ScenePath);
    var impact = _scene?.Instantiate<HairballImpactEffect>();
    if (impact == null) return;
    var key = (evt.Tick, evt.ProjectileId, evt.GetContentHash());
    _impacts[key] = impact;
    impact.TreeExiting += () => {
      if (_impacts.TryGetValue(key, out var current) && current == impact)
        _impacts.Remove(key);
    };
    _layer.AddChild(impact);
    impact.GlobalPosition = evt.Position.ToVector3() + Vector3.Up * _height;
    impact.Scale = Vector3.One * Mathf.Max(projectile.Radius.ToFloat() / BaseRadius, 0.01f);
  }

  private void HandleConfirmed(SkillProjectileDespawnedEvent evt) => _projectiles.Remove(evt.ProjectileId);

  private void HandleSpawnCanceled(SkillProjectileSpawnedEvent evt) {
    if (_projectiles.TryGetValue(evt.ProjectileId, out var projectile) &&
        projectile.Tick == evt.Tick && projectile.Hash == evt.GetContentHash())
      _projectiles.Remove(evt.ProjectileId);
  }

  private void HandleImpactCanceled(SkillProjectileDespawnedEvent evt) {
    var key = (evt.Tick, evt.ProjectileId, evt.GetContentHash());
    if (_impacts.Remove(key, out var impact) && GodotObject.IsInstanceValid(impact))
      impact.QueueFree();
  }
}
