using System;
using System.Collections.Generic;
using Godot;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Client.Scripts.View;

public sealed class SnailTrailEffects {
  private static PackedScene _scene;
  private readonly Dictionary<int, ActiveSegment> _active = new();
  private readonly Stack<Node3D> _idle = new();
  private readonly List<int> _staleIds = new();
  private Node3D _layer;

  public void Attach(Node3D layer) {
    Detach();
    _layer = layer;
  }

  public void Detach() {
    foreach (var segment in _active.Values) segment.Node.QueueFree();
    _active.Clear();
    while (_idle.Count > 0) _idle.Pop().QueueFree();
    _staleIds.Clear();
    _layer = null;
  }

  public void Reconcile(ref Frame frame) {
    if (_layer == null) return;
    _staleIds.Clear();
    foreach (var id in _active.Keys) _staleIds.Add(id);

    var filter = frame.Filter<TrailSegment, TransformComponent>();
    while (filter.Next(out var entity)) {
      ref readonly var segment = ref frame.GetReadOnly<TrailSegment>(entity);
      if (segment.SkillAssetId == AssetIds.SkillSnailheadSecondary && segment.ExpiryTick > frame.Tick)
        _staleIds.Remove(segment.SegmentId);
    }
    foreach (var id in _staleIds) {
      var node = _active[id].Node;
      node.Call("stop");
      _idle.Push(node);
      _active.Remove(id);
    }

    var target = PoolTarget(ref frame);
    ResizePool(target);
    filter = frame.Filter<TrailSegment, TransformComponent>();
    while (filter.Next(out var entity)) {
      ref readonly var segment = ref frame.GetReadOnly<TrailSegment>(entity);
      if (segment.SkillAssetId != AssetIds.SkillSnailheadSecondary || segment.ExpiryTick <= frame.Tick ||
          !frame.AssetRegistry.TryGet<SkillAsset>(segment.SkillAssetId, out var skill)) continue;

      if (!_active.TryGetValue(segment.SegmentId, out var effect)) {
        effect = new ActiveSegment(_idle.Count > 0 ? _idle.Pop() : Create());
        _active.Add(segment.SegmentId, effect);
      }
      var lifetimeTicks = TickMath.MsToTicksCeil(ref frame, skill.TrailDurationMsAtRank(segment.Rank));
      var durationTicks = lifetimeTicks > 0 ? lifetimeTicks : 1;
      var tickIntervalMs = TickMath.DeltaTimeMs(ref frame);
      effect.NeedsConfigure = effect.Width != segment.Width || effect.DurationTicks != durationTicks ||
                              effect.TickIntervalMs != tickIntervalMs;
      effect.Position = frame.GetReadOnly<TransformComponent>(entity).Position;
      effect.Width = segment.Width;
      effect.Seed = segment.SegmentId;
      effect.DurationTicks = durationTicks;
      effect.RemainingTicks = segment.ExpiryTick - frame.Tick;
      effect.TickIntervalMs = tickIntervalMs;
    }
    ResizePool(target);
    ConfigureVisuals();
  }

  private void ConfigureVisuals() {
    foreach (var effect in _active.Values) {
      effect.Node.GlobalPosition = effect.Position.ToVector3();
      if (!effect.NeedsConfigure) continue;
      var duration = effect.DurationTicks * effect.TickIntervalMs / 1000f;
      effect.Node.Call("configure_segment", effect.Width.ToFloat(), duration,
        (float)effect.Seed, false);
      effect.NeedsConfigure = false;
    }
    ApplyVisual(0f);
  }

  public void ApplyVisual(float secondsSinceTick) {
    foreach (var effect in _active.Values) {
      var ticksSinceTick = secondsSinceTick * 1000f / effect.TickIntervalMs;
      var progress = 1f - (effect.RemainingTicks - ticksSinceTick) / effect.DurationTicks;
      effect.Node.Call("set_progress", progress);
    }
  }

  private static int PoolTarget(ref Frame frame) {
    var count = 0;
    var heroes = frame.Filter<Hero, Skills>();
    while (heroes.Next(out var entity)) {
      ref readonly var skills = ref frame.GetReadOnly<Skills>(entity);
      for (var slot = 0; slot < Skills.MaxSlots; slot++) {
        if (skills.GetSkillAssetId(slot) == AssetIds.SkillSnailheadSecondary &&
            frame.AssetRegistry.TryGet<SkillAsset>(AssetIds.SkillSnailheadSecondary, out var skill))
          count += skill.TrailSegmentCount > 0 ? skill.TrailSegmentCount : 0;
      }
    }
    return count;
  }

  private void ResizePool(int target) {
    var idleTarget = Math.Max(target - _active.Count, 0); // One cast per hero; overflow rents on demand.
    while (_idle.Count > idleTarget) _idle.Pop().QueueFree();
    while (_idle.Count < idleTarget) _idle.Push(Create());
  }

  private Node3D Create() {
    _scene ??= GD.Load<PackedScene>(SkillEffectCatalog.SnailTrailScenePath);
    var node = _scene.Instantiate<Node3D>();
    _layer.AddChild(node);
    node.Call("stop");
    return node;
  }

  private sealed class ActiveSegment(Node3D node) {
    public readonly Node3D Node = node;
    public FPVector3 Position;
    public FP64 Width;
    public int Seed;
    public bool NeedsConfigure;
    public int DurationTicks;
    public int RemainingTicks;
    public int TickIntervalMs;
  }
}
