using System.Collections.Generic;
using Godot;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Godot;

namespace Meesles.Avalon.Client.Scripts.View;

public sealed class ChargeCircleEffects {
  private static PackedScene _scene;
  private readonly Dictionary<int, ActiveCircle> _active = new();
  private readonly Dictionary<int, ActiveCircle> _fading = new();
  private readonly Stack<Node3D> _pool = new();
  private readonly List<int> _staleIds = new();
  private Node3D _layer;
  private EntityViewUpdaterNode _view;

  public void Attach(Node3D layer, EntityViewUpdaterNode view) {
    Detach();
    _layer = layer;
    _view = view;
  }

  public void Detach() {
    foreach (var effect in _active.Values) effect.Node.QueueFree();
    _active.Clear();
    foreach (var effect in _fading.Values) effect.Node.QueueFree();
    _fading.Clear();
    while (_pool.Count > 0) _pool.Pop().QueueFree();
    _staleIds.Clear();
    _layer = null;
    _view = null;
  }

  public void Reconcile(ref Frame frame) {
    if (_layer == null) return;
    _staleIds.Clear();
    foreach (var unitId in _active.Keys) _staleIds.Add(unitId);

    var channels = frame.Filter<SkillChannel, TransformComponent>();
    while (channels.Next(out var entity)) {
      ref readonly var channel = ref frame.GetReadOnly<SkillChannel>(entity);
      if (!channel.IsActive || channel.SourceId != AssetIds.SkillSnailheadUltimate ||
          channel.CompleteTick <= frame.Tick ||
          !frame.AssetRegistry.TryGet<SkillAsset>(channel.SourceId, out var skill)) continue;
      Sync(ref frame, entity, skill.AreaRadius,
        TickMath.MsToTicksCeil(ref frame, skill.ChargeDurationMsAtRank(channel.Rank)), channel.CompleteTick);
    }

    foreach (var unitId in _staleIds) {
      var effect = _active[unitId];
      _active.Remove(unitId);
      if (frame.Tick < effect.CompleteTick) {
        _fading.Add(unitId, effect);
        effect.Node.Call("fade_out");
      }
      else {
        effect.Node.Call("stop");
        _pool.Push(effect.Node);
      }
    }
    ConfigureVisuals();
  }

  private void Sync(ref Frame frame, EntityRef entity, FP64 radius, int durationTicks, int completeTick) {
    var unitId = UnitLookup.GetUnitId(ref frame, entity);
    _staleIds.Remove(unitId);
    if (!_active.TryGetValue(unitId, out var effect)) {
      if (!_fading.Remove(unitId, out effect))
        effect = new ActiveCircle(_pool.Count > 0 ? _pool.Pop() : Create());
      _active.Add(unitId, effect);
    }
    effect.Position = frame.GetReadOnly<TransformComponent>(entity).Position;
    effect.Radius = radius;
    effect.DurationTicks = durationTicks > 0 ? durationTicks : 1;
    effect.RemainingTicks = completeTick - frame.Tick;
    effect.CompleteTick = completeTick;
    effect.TickIntervalMs = TickMath.DeltaTimeMs(ref frame);
    effect.Follow = null;
    _view?.ViewsByUnitId.TryGetValue(unitId, out effect.Follow);
  }

  private void ConfigureVisuals() {
    foreach (var effect in _active.Values)
      effect.Node.Call("configure", effect.Radius.ToFloat());
    ApplyVisual(0f);
  }

  public void ApplyVisual(float secondsSinceTick) {
    foreach (var effect in _active.Values) {
      effect.Node.GlobalPosition = GodotObject.IsInstanceValid(effect.Follow)
        ? effect.Follow.GlobalPosition : effect.Position.ToVector3();
      var ticksSinceTick = secondsSinceTick * 1000f / effect.TickIntervalMs;
      var progress = 1f - (effect.RemainingTicks - ticksSinceTick) / effect.DurationTicks;
      effect.Node.Call("set_progress", progress);
    }
  }

  private Node3D Create() {
    _scene ??= GD.Load<PackedScene>(SkillEffectCatalog.ChargeCircleScenePath);
    var node = _scene.Instantiate<Node3D>();
    _layer.AddChild(node);
    node.Connect("fade_finished", Callable.From(() => ReleaseFaded(node)));
    return node;
  }

  private void ReleaseFaded(Node3D node) {
    var unitId = 0;
    var found = false;
    foreach (var entry in _fading) {
      if (entry.Value.Node != node) continue;
      unitId = entry.Key;
      found = true;
      break;
    }
    if (!found) return;
    _fading.Remove(unitId);
    _pool.Push(node);
  }

  private sealed class ActiveCircle(Node3D node) {
    public readonly Node3D Node = node;
    public EntityViewNode Follow;
    public FPVector3 Position;
    public FP64 Radius;
    public int DurationTicks;
    public int RemainingTicks;
    public int CompleteTick;
    public int TickIntervalMs;
  }
}
