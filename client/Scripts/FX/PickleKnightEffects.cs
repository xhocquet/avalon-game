using System;
using System.Collections.Generic;
using Godot;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Assets;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;

namespace Meesles.Avalon.Client.Scripts.View;

public sealed class PickleKnightEffects {
  private const float ExploosionBaseRadius = 5f;
  private static PackedScene _refreshScene;
  private static PackedScene _exploosionScene;

  private readonly HashSet<GpuParticles3D> _active = new();
  private IDisposable _castSub;
  private IKlothoEngine _engine;
  private Node3D _layer;

  public void Attach(SimEventHub events, Node3D layer, IKlothoEngine engine) {
    Detach();
    _layer = layer;
    _engine = engine;
    _castSub = events.OnConfirmed<SkillCastEvent>(HandleCast);
  }

  public void Detach() {
    _castSub?.Dispose();
    _castSub = null;
    foreach (var effect in _active)
      if (GodotObject.IsInstanceValid(effect)) effect.QueueFree();
    _active.Clear();
    _layer = null;
    _engine = null;
  }

  private void HandleCast(SkillCastEvent evt) {
    if (_layer == null) return;

    PackedScene scene;
    switch (evt.SkillAssetId) {
      case AssetIds.SkillPickleKnightTertiary:
        scene = _refreshScene ??= GD.Load<PackedScene>(SkillEffectCatalog.PickleKnightRefreshScenePath);
        break;
      case AssetIds.SkillPickleKnightUltimate:
        scene = _exploosionScene ??= GD.Load<PackedScene>(SkillEffectCatalog.PickleKnightExploosionScenePath);
        break;
      default:
        return;
    }

    var effect = scene?.Instantiate<GpuParticles3D>();
    if (effect == null) return;

    effect.Emitting = false;
    effect.Position = evt.Position.ToVector3() + Vector3.Up;
    var registry = _engine?.PredictedFrame.Frame?.AssetRegistry;
    if (evt.SkillAssetId == AssetIds.SkillPickleKnightUltimate &&
        registry != null && registry.TryGet<SkillAsset>(evt.SkillAssetId, out var skill))
      effect.Scale = Vector3.One * Mathf.Max(skill.AreaRadius.ToFloat() / ExploosionBaseRadius, 0.01f);

    _active.Add(effect);
    effect.Finished += () => {
      _active.Remove(effect);
      effect.QueueFree();
    };
    _layer.AddChild(effect);
    effect.Restart();
  }
}
