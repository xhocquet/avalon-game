using System;
using System.Collections.Generic;
using Godot;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Godot;

namespace Meesles.Avalon.Client.Scripts.View;

public sealed class SkinwalkerEffects {
  private static PackedScene _scene;
  private readonly HashSet<HealingSkillEffect> _active = new();
  private readonly Stack<HealingSkillEffect> _pool = new();
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
    _castSub = events.OnConfirmed<SkillCastEvent>(HandleCast);
  }

  public void Detach() {
    _castSub?.Dispose();
    _castSub = null;
    foreach (var effect in _active) Destroy(effect);
    _active.Clear();
    while (_pool.Count > 0) Destroy(_pool.Pop());
    _engine = null;
    _layer = null;
    _view = null;
  }

  private void HandleCast(SkillCastEvent evt) {
    if (_layer == null || evt.SkillAssetId != AssetIds.SkillSkinwalkerTertiary) return;
    var frame = _engine?.PredictedFrame.Frame;
    if (frame == null || !UnitLookup.TryGetEntityByUnitId(ref frame, evt.UnitId, out var caster) ||
        !frame.Has<Stats>(caster)) return;

    EntityViewNode follow = null;
    _view?.ViewsByUnitId.TryGetValue(evt.UnitId, out follow);
    var radius = frame.GetReadOnly<Stats>(caster).GameplayRadius.ToFloat();
    var effect = _pool.Count > 0 ? _pool.Pop() : Create();
    _active.Add(effect);
    effect.Play(evt.Position.ToVector3(), Mathf.Max(radius, 0.01f), follow);
  }

  private HealingSkillEffect Create() {
    _scene ??= GD.Load<PackedScene>(SkillEffectCatalog.SkinwalkerEatToSurviveScenePath);
    var effect = _scene.Instantiate<HealingSkillEffect>();
    effect.Finished = Release;
    _layer.AddChild(effect);
    return effect;
  }

  private void Release(HealingSkillEffect effect) {
    if (_active.Remove(effect)) _pool.Push(effect);
  }

  private static void Destroy(HealingSkillEffect effect) {
    if (!GodotObject.IsInstanceValid(effect)) return;
    effect.Finished = null;
    effect.ResetVisual();
    effect.QueueFree();
  }
}
