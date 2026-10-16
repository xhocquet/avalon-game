using System;
using System.Collections.Generic;
using Godot;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Godot;

namespace Meesles.Avalon.Client.Scripts.View;

public sealed class PickleKnightEffects {
  private static PackedScene _refreshScene;
  private static PackedScene _exploosionScene;
  private static PackedScene _doubleDipScene;
  private readonly HashSet<SheenSkillEffect> _activeSheens = new();
  private readonly Stack<SheenSkillEffect> _sheenPool = new();
  private readonly Dictionary<HealingSkillEffect, Stack<HealingSkillEffect>> _active = new();
  private readonly Stack<HealingSkillEffect> _refreshPool = new();
  private readonly Stack<HealingSkillEffect> _exploosionPool = new();
  private IDisposable _castSub;
  private IKlothoEngine _engine;
  private Node3D _layer;
  private EntityViewUpdaterNode _view;

  public void Attach(SimEventHub events, Node3D layer, IKlothoEngine engine, EntityViewUpdaterNode view) {
    Detach();
    _layer = layer;
    _engine = engine;
    _view = view;
    _sheenPool.Push(CreateSheen());
    _refreshPool.Push(Create(ref _refreshScene, SkillEffectCatalog.PickleKnightRefreshScenePath));
    _exploosionPool.Push(Create(ref _exploosionScene, SkillEffectCatalog.PickleKnightExploosionScenePath));
    _castSub = events.OnConfirmed<SkillCastEvent>(HandleCast);
  }

  public void Detach() {
    _castSub?.Dispose();
    _castSub = null;
    foreach (var effect in _active.Keys) Destroy(effect);
    _active.Clear();
    while (_refreshPool.Count > 0) Destroy(_refreshPool.Pop());
    while (_exploosionPool.Count > 0) Destroy(_exploosionPool.Pop());
    foreach (var effect in _activeSheens) Destroy(effect);
    _activeSheens.Clear();
    while (_sheenPool.Count > 0) Destroy(_sheenPool.Pop());
    _layer = null;
    _engine = null;
    _view = null;
  }

  private void HandleCast(SkillCastEvent evt) {
    if (_layer == null || (evt.SkillAssetId != AssetIds.SkillPickleKnightSecondary &&
                           evt.SkillAssetId != AssetIds.SkillPickleKnightTertiary &&
                           evt.SkillAssetId != AssetIds.SkillPickleKnightUltimate)) return;
    var frame = _engine?.PredictedFrame.Frame;
    if (frame == null || !frame.AssetRegistry.TryGet<SkillAsset>(evt.SkillAssetId, out var skill)) return;

    if (evt.SkillAssetId == AssetIds.SkillPickleKnightSecondary) {
      if (!UnitLookup.TryGetEntityByUnitId(ref frame, evt.UnitId, out var caster) ||
          !frame.Has<TransformComponent>(caster)) return;
      EntityViewNode casterView = null;
      _view?.ViewsByUnitId.TryGetValue(evt.UnitId, out casterView);
      var sheen = _sheenPool.Count > 0 ? _sheenPool.Pop() : CreateSheen();
      _activeSheens.Add(sheen);
      sheen.Play(evt.Position.ToVector3(), frame.GetReadOnly<TransformComponent>(caster).Rotation.ToFloat(), casterView);
      return;
    }

    var refresh = evt.SkillAssetId == AssetIds.SkillPickleKnightTertiary;
    EntityViewNode follow = null;
    var radius = skill.AreaRadius.ToFloat();
    if (refresh) {
      if (!UnitLookup.TryGetEntityByUnitId(ref frame, evt.UnitId, out var caster) || !frame.Has<Stats>(caster))
        return;
      radius = frame.GetReadOnly<Stats>(caster).GameplayRadius.ToFloat();
      _view?.ViewsByUnitId.TryGetValue(evt.UnitId, out follow);
    }
    var pool = refresh ? _refreshPool : _exploosionPool;
    var effect = pool.Count > 0 ? pool.Pop() : refresh
      ? Create(ref _refreshScene, SkillEffectCatalog.PickleKnightRefreshScenePath)
      : Create(ref _exploosionScene, SkillEffectCatalog.PickleKnightExploosionScenePath);
    _active.Add(effect, pool);
    effect.Play(evt.Position.ToVector3(), Mathf.Max(radius, 0.01f), follow);
  }

  private HealingSkillEffect Create(ref PackedScene scene, string path) {
    scene ??= GD.Load<PackedScene>(path);
    var effect = scene.Instantiate<HealingSkillEffect>();
    effect.Finished = Release;
    _layer.AddChild(effect);
    return effect;
  }

  private void Release(HealingSkillEffect effect) {
    if (_active.Remove(effect, out var pool)) pool.Push(effect);
  }

  private SheenSkillEffect CreateSheen() {
    _doubleDipScene ??= GD.Load<PackedScene>(SkillEffectCatalog.PickleKnightDoubleDipScenePath);
    var effect = _doubleDipScene.Instantiate<SheenSkillEffect>();
    effect.Finished = Release;
    _layer.AddChild(effect);
    return effect;
  }

  private void Release(SheenSkillEffect effect) {
    if (_activeSheens.Remove(effect)) _sheenPool.Push(effect);
  }

  private static void Destroy(SheenSkillEffect effect) {
    if (!GodotObject.IsInstanceValid(effect)) return;
    effect.Finished = null;
    effect.ResetVisual();
    effect.QueueFree();
  }

  private static void Destroy(HealingSkillEffect effect) {
    if (!GodotObject.IsInstanceValid(effect)) return;
    effect.Finished = null;
    effect.ResetVisual();
    effect.QueueFree();
  }
}
