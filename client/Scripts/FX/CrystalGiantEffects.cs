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

public sealed class CrystalGiantEffects {
  private static PackedScene _scene;
  private static PackedScene _punchScene;
  private readonly HashSet<HitSparkSkillEffect> _activePunches = new();
  private readonly Stack<HitSparkSkillEffect> _punchPool = new();
  private readonly HashSet<SprintSkillEffect> _active = new();
  private readonly Stack<SprintSkillEffect> _pool = new();
  private IDisposable _castSub;
  private IKlothoEngine _engine;
  private Node3D _layer;
  private EntityViewUpdaterNode _view;

  public void Attach(SimEventHub events, Node3D layer, IKlothoEngine engine, EntityViewUpdaterNode view) {
    Detach();
    _layer = layer;
    _engine = engine;
    _view = view;
    _pool.Push(Create());
    _punchPool.Push(CreatePunch());
    _castSub = events.OnConfirmed<SkillCastEvent>(HandleCast);
  }

  public void Detach() {
    _castSub?.Dispose();
    _castSub = null;
    foreach (var effect in _active) Destroy(effect);
    _active.Clear();
    while (_pool.Count > 0) Destroy(_pool.Pop());
    foreach (var effect in _activePunches) Destroy(effect);
    _activePunches.Clear();
    while (_punchPool.Count > 0) Destroy(_punchPool.Pop());
    _engine = null;
    _layer = null;
    _view = null;
  }

  private void HandleCast(SkillCastEvent evt) {
    if (_layer == null || (evt.SkillAssetId != AssetIds.SkillCrystalGiantPrimary &&
                           evt.SkillAssetId != AssetIds.SkillCrystalGiantSecondary)) return;
    var frame = _engine?.PredictedFrame.Frame;
    if (frame == null || !UnitLookup.TryGetEntityByUnitId(ref frame, evt.UnitId, out var caster) ||
        !frame.Has<Stats>(caster)) return;

    EntityViewNode follow = null;
    _view?.ViewsByUnitId.TryGetValue(evt.UnitId, out follow);
    if (evt.SkillAssetId == AssetIds.SkillCrystalGiantPrimary) {
      if (!frame.Has<TransformComponent>(caster)) return;
      var punch = _punchPool.Count > 0 ? _punchPool.Pop() : CreatePunch();
      _activePunches.Add(punch);
      punch.Play(evt.Position.ToVector3(), frame.GetReadOnly<TransformComponent>(caster).Rotation.ToFloat(), follow);
      return;
    }
    var radius = Mathf.Max(frame.GetReadOnly<Stats>(caster).GameplayRadius.ToFloat(), 0.01f);
    var effect = _pool.Count > 0 ? _pool.Pop() : Create();
    _active.Add(effect);
    effect.Play(evt.Position.ToVector3(), radius, follow);
  }

  private SprintSkillEffect Create() {
    _scene ??= GD.Load<PackedScene>(SkillEffectCatalog.CrystalGiantHardenScenePath);
    var effect = _scene.Instantiate<SprintSkillEffect>();
    effect.Finished = Release;
    _layer.AddChild(effect);
    return effect;
  }

  private void Release(SprintSkillEffect effect) {
    if (_active.Remove(effect)) _pool.Push(effect);
  }

  private HitSparkSkillEffect CreatePunch() {
    _punchScene ??= GD.Load<PackedScene>(SkillEffectCatalog.CrystalGiantSpikyPunchScenePath);
    var effect = _punchScene.Instantiate<HitSparkSkillEffect>();
    effect.Finished = Release;
    _layer.AddChild(effect);
    return effect;
  }

  private void Release(HitSparkSkillEffect effect) {
    if (_activePunches.Remove(effect)) _punchPool.Push(effect);
  }

  private static void Destroy(HitSparkSkillEffect effect) {
    if (!GodotObject.IsInstanceValid(effect)) return;
    effect.Finished = null;
    effect.ResetVisual();
    effect.QueueFree();
  }

  private static void Destroy(SprintSkillEffect effect) {
    if (!GodotObject.IsInstanceValid(effect)) return;
    effect.Finished = null;
    effect.ResetVisual();
    effect.QueueFree();
  }
}

