using System;
using System.Collections.Generic;
using Godot;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Assets;
using xpTURN.Klotho.Deterministic.Math;

namespace Meesles.Avalon.Client.Scripts.View;

public sealed class ChrysalisEffects {
  private static PackedScene _scene;
  private readonly Dictionary<(int Tick, int UnitId, long Hash), Burst> _active = new();
  private readonly Stack<Burst> _pool = new();
  private IDisposable _detonatedSub;
  private IDisposable _canceledSub;
  private Node3D _layer;

  public void Attach(SimEventHub events, Node3D layer) {
    Detach();
    _layer = layer;
    _detonatedSub = events.OnFx<SkillChargeDetonatedEvent>(HandleDetonated);
    _canceledSub = events.OnCanceled<SkillChargeDetonatedEvent>(HandleCanceled);
  }

  public void Detach() {
    _detonatedSub?.Dispose();
    _canceledSub?.Dispose();
    _detonatedSub = _canceledSub = null;
    foreach (var effect in _active.Values) effect.Node.QueueFree();
    _active.Clear();
    while (_pool.Count > 0) _pool.Pop().Node.QueueFree();
    _layer = null;
  }

  private void HandleDetonated(SkillChargeDetonatedEvent evt) {
    if (_layer == null || evt.SkillAssetId != AssetIds.SkillCrystalGiantUltimate) return;
    var key = (evt.Tick, evt.CasterUnitId, evt.GetContentHash());
    if (_active.ContainsKey(key)) return;
    var effect = _pool.Count > 0 ? _pool.Pop() : Create();
    effect.Key = key;
    _active.Add(key, effect);
    effect.Node.GlobalPosition = evt.Position.ToVector3();
    effect.Node.Call("play", evt.Radius.ToFloat());
  }

  private void HandleCanceled(SkillChargeDetonatedEvent evt) {
    var key = (evt.Tick, evt.CasterUnitId, evt.GetContentHash());
    if (_active.TryGetValue(key, out var effect)) Release(effect);
  }

  private Burst Create() {
    _scene ??= GD.Load<PackedScene>(SkillEffectCatalog.ChrysalisBurstScenePath);
    var effect = new Burst(_scene.Instantiate<Node3D>());
    _layer.AddChild(effect.Node);
    effect.Node.Connect("finished", Callable.From(() => Release(effect)));
    return effect;
  }

  private void Release(Burst effect) {
    if (!_active.Remove(effect.Key)) return;
    effect.Node.Call("stop");
    _pool.Push(effect);
  }

  private sealed class Burst(Node3D node) {
    public readonly Node3D Node = node;
    public (int Tick, int UnitId, long Hash) Key;
  }
}
