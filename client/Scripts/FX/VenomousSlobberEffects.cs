using System;
using System.Collections.Generic;
using Godot;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Assets;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Godot;

namespace Meesles.Avalon.Client.Scripts.View;

public sealed class VenomousSlobberEffects {
  private static PackedScene _scene;
  private readonly HashSet<Node3D> _active = new();
  private readonly Stack<Node3D> _idle = new();
  private IDisposable _castSub;
  private IKlothoEngine _engine;
  private Node3D _layer;

  public void Attach(SimEventHub events, Node3D layer, IKlothoEngine engine) {
    Detach();
    _layer = layer;
    _engine = engine;
    _idle.Push(Create());
    _castSub = events.OnConfirmed<SkillCastEvent>(HandleCast);
  }

  public void Detach() {
    _castSub?.Dispose();
    _castSub = null;
    foreach (var node in _active) Destroy(node);
    _active.Clear();
    while (_idle.Count > 0) Destroy(_idle.Pop());
    _layer = null;
    _engine = null;
  }

  private void HandleCast(SkillCastEvent evt) {
    if (_layer == null || evt.SkillAssetId != AssetIds.SkillSnailheadPrimary) return;
    var frame = _engine?.PredictedFrame.Frame;
    if (frame == null || !frame.AssetRegistry.TryGet<SkillAsset>(evt.SkillAssetId, out var skill)) return;
    UnitLookup.TryGetEntityByUnitId(ref frame, evt.UnitId, out var caster);
    var direction = SkillAim.Direction(ref frame, caster, evt.Position, evt.TargetPosition).ToVector3();
    var effect = _idle.Count > 0 ? _idle.Pop() : Create();
    _active.Add(effect);
    var position = evt.Position.ToVector3();
    effect.GlobalPosition = position;
    effect.LookAt(position + direction, Vector3.Up);
    effect.Call("configure_cone", skill.ConeRange.ToFloat(), skill.ConeAngleDegrees.ToFloat());
    effect.Call("play");
  }

  private Node3D Create() {
    _scene ??= GD.Load<PackedScene>(SkillEffectCatalog.VenomousSlobberScenePath);
    var node = _scene.Instantiate<Node3D>();
    _layer.AddChild(node);
    node.Call("stop");
    node.Connect("finished", Callable.From(() => Release(node)));
    return node;
  }

  private void Release(Node3D node) {
    if (_active.Remove(node)) _idle.Push(node);
  }

  private static void Destroy(Node3D node) {
    if (!GodotObject.IsInstanceValid(node)) return;
    node.Call("stop");
    node.QueueFree();
  }
}
