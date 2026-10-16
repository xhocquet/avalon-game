using System.Collections.Generic;
using Godot;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Godot;

namespace Meesles.Avalon.Client.Scripts.View;

public sealed class MoltBuffEffects {
  private static PackedScene _scene;
  private readonly Dictionary<int, ActiveBuff> _active = new();
  private readonly Stack<Node3D> _pool = new();
  private readonly List<int> _staleIds = new();
  private Node3D _layer;
  private EntityViewUpdaterNode _view;

  public void Attach(Node3D layer, EntityViewUpdaterNode view) {
    Detach();
    _layer = layer;
    _view = view;
    _pool.Push(Create());
  }

  public void Detach() {
    foreach (var effect in _active.Values) effect.Node.QueueFree();
    _active.Clear();
    while (_pool.Count > 0) _pool.Pop().QueueFree();
    _staleIds.Clear();
    _layer = null;
    _view = null;
  }

  public void Reconcile(ref Frame frame) {
    if (_layer == null ||
        !frame.AssetRegistry.TryGet<SkillAsset>(AssetIds.SkillSnailheadUltimate, out var skill)) return;
    _staleIds.Clear();
    foreach (var unitId in _active.Keys) _staleIds.Add(unitId);

    var filter = frame.Filter<StatBuffs, Stats, TransformComponent>();
    while (filter.Next(out var entity)) {
      ref readonly var buffs = ref frame.GetReadOnly<StatBuffs>(entity);
      var expiryTick = 0;
      for (var i = 0; i < StatBuffs.MaxEntries; i++)
        if (buffs.GetSourceId(i) == skill.AssetId && buffs.GetExpiryTick(i) > frame.Tick &&
            buffs.GetExpiryTick(i) > expiryTick)
          expiryTick = buffs.GetExpiryTick(i);
      if (expiryTick == 0) continue;

      var unitId = UnitLookup.GetUnitId(ref frame, entity);
      _staleIds.Remove(unitId);
      if (!_active.TryGetValue(unitId, out var effect)) {
        effect = new ActiveBuff(_pool.Count > 0 ? _pool.Pop() : Create());
        _active.Add(unitId, effect);
      }
      var rank = frame.Has<Skills>(entity)
        ? frame.GetReadOnly<Skills>(entity).GetRank((int)SkillSlot.Ultimate) : 1;
      var durationTicks = TickMath.MsToTicksCeil(ref frame, skill.BuffDurationMsAtRank(rank));
      effect.ElapsedTicks = frame.Tick - (expiryTick - durationTicks);
      effect.TickIntervalMs = TickMath.DeltaTimeMs(ref frame);
      effect.Position = frame.GetReadOnly<TransformComponent>(entity).Position;
      effect.Radius = frame.GetReadOnly<Stats>(entity).GameplayRadius;
      effect.Follow = null;
      _view?.ViewsByUnitId.TryGetValue(unitId, out effect.Follow);
    }

    foreach (var unitId in _staleIds) {
      var node = _active[unitId].Node;
      node.Call("stop");
      _pool.Push(node);
      _active.Remove(unitId);
    }
    ConfigureVisuals();
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
      var elapsed = effect.ElapsedTicks * effect.TickIntervalMs / 1000f + secondsSinceTick;
      effect.Node.Call("set_elapsed", elapsed);
    }
  }

  private Node3D Create() {
    _scene ??= GD.Load<PackedScene>(SkillEffectCatalog.SnailheadMoltBuffScenePath);
    var node = _scene.Instantiate<Node3D>();
    _layer.AddChild(node);
    return node;
  }

  private sealed class ActiveBuff(Node3D node) {
    public readonly Node3D Node = node;
    public EntityViewNode Follow;
    public FPVector3 Position;
    public FP64 Radius;
    public int ElapsedTicks;
    public int TickIntervalMs;
  }
}
