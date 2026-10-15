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

public sealed class AllHairWizardEffects {
  private static PackedScene _scene;
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
    if (_layer == null || evt.SkillAssetId != AssetIds.SkillHairyWizardTertiary) return;
    var frame = _engine?.PredictedFrame.Frame;
    if (frame == null || !UnitLookup.TryGetEntityByUnitId(ref frame, evt.UnitId, out var caster) ||
        !frame.Has<Stats>(caster)) return;

    EntityViewNode follow = null;
    _view?.ViewsByUnitId.TryGetValue(evt.UnitId, out follow);
    var radius = Mathf.Max(frame.GetReadOnly<Stats>(caster).GameplayRadius.ToFloat(), 0.01f);
    var effect = _pool.Count > 0 ? _pool.Pop() : Create();
    _active.Add(effect);
    effect.Play(evt.Position.ToVector3(), radius, follow);
  }

  private SprintSkillEffect Create() {
    _scene ??= GD.Load<PackedScene>(SkillEffectCatalog.AllHairWizardCloseShaveScenePath);
    var effect = _scene.Instantiate<SprintSkillEffect>();
    effect.Finished = Release;
    _layer.AddChild(effect);
    return effect;
  }

  private void Release(SprintSkillEffect effect) {
    if (_active.Remove(effect)) _pool.Push(effect);
  }

  private static void Destroy(SprintSkillEffect effect) {
    if (!GodotObject.IsInstanceValid(effect)) return;
    effect.Finished = null;
    effect.ResetVisual();
    effect.QueueFree();
  }
}
