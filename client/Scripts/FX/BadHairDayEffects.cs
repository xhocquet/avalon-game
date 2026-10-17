using System.Collections.Generic;
using Godot;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Godot;

namespace Meesles.Avalon.Client.Scripts.View;

public sealed class BadHairDayEffects {
  private const float Height = 0.75f;
  private static PackedScene _scene;
  private readonly Dictionary<int, ActiveHair> _active = new();
  private readonly Stack<ActiveHair> _pool = new();
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
    while (_pool.Count > 0) _pool.Pop().Node.QueueFree();
    _staleIds.Clear();
    _layer = null;
    _view = null;
  }

  public void Reconcile(ref Frame frame) {
    if (_layer == null) return;
    _staleIds.Clear();
    foreach (var unitId in _active.Keys) _staleIds.Add(unitId);

    var charges = frame.Filter<SkillCharge, TransformComponent>();
    while (charges.Next(out var entity)) {
      ref readonly var charge = ref frame.GetReadOnly<SkillCharge>(entity);
      if (!charge.IsCharging || charge.SourceId != AssetIds.SkillHairyWizardUltimate ||
          charge.DetonateTick <= frame.Tick) continue;

      var unitId = UnitLookup.GetUnitId(ref frame, entity);
      if (unitId == 0) continue;
      _staleIds.Remove(unitId);
      if (!_active.TryGetValue(unitId, out var effect)) {
        effect = _pool.Count > 0 ? _pool.Pop() : Create();
        _active.Add(unitId, effect);
      }
      if (effect.Radius != charge.Radius) {
        effect.Radius = charge.Radius;
        effect.Node.Set("length", effect.Radius.ToFloat());
      }
      effect.Position = frame.GetReadOnly<TransformComponent>(entity).Position;
      effect.Follow = null;
      _view?.ViewsByUnitId.TryGetValue(unitId, out effect.Follow);
      effect.Node.Show();
    }

    foreach (var unitId in _staleIds) {
      var effect = _active[unitId];
      effect.Node.Hide();
      effect.Follow = null;
      _pool.Push(effect);
      _active.Remove(unitId);
    }
    ApplyVisual();
  }

  public void ApplyVisual() {
    foreach (var effect in _active.Values) {
      var position = GodotObject.IsInstanceValid(effect.Follow)
        ? effect.Follow.GlobalPosition : effect.Position.ToVector3();
      effect.Node.GlobalPosition = position + Vector3.Up * Height;
    }
  }

  private ActiveHair Create() {
    _scene ??= GD.Load<PackedScene>(SkillEffectCatalog.AllHairWizardBadHairDayScenePath);
    var node = _scene.Instantiate<Node3D>();
    _layer.AddChild(node);
    node.Hide();
    return new ActiveHair(node);
  }

  private sealed class ActiveHair(Node3D node) {
    public readonly Node3D Node = node;
    public EntityViewNode Follow;
    public FPVector3 Position;
    public FP64 Radius;
  }
}
