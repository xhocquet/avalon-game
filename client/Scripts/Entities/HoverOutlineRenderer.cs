using System.Collections.Generic;
using Godot;

namespace Meesles.Avalon.Client.Scripts.Entities;

public partial class HoverOutlineRenderer : Node {
  private readonly Dictionary<Material, BaseMaterial3D> _materials = new();
  private readonly List<(MeshInstance3D Mesh, int Surface, Material Original)> _overrides = new();
  private Camera3D _camera;
  private Compositor _compositor;
  private Compositor _activeCompositor;
  private CompositorEffect _effect;
  private MaskHoverOutlineRenderer _fallback;
  private Node3D _target;

  public override void _Ready() {
    _camera = GetParent<Camera3D>();
    if (OS.GetEnvironment("AVALON_HOVER_OUTLINE") == "mask" ||
        RenderingServer.GetCurrentRenderingMethod() != "forward_plus" || GetViewport().Msaa3D != Viewport.Msaa.Disabled) {
      _fallback = new MaskHoverOutlineRenderer { Name = "MaskFallback" };
      _camera.AddChild(_fallback);
      SetProcess(false);
      return;
    }
    _effect = (CompositorEffect)GD.Load<CompositorEffect>("res://Assets/FX/StencilOutline/HoverStencilOutline.tres").Duplicate();
    _effect.Enabled = false;
    _compositor = _camera.Compositor;
    _activeCompositor = new Compositor();
    _camera.Compositor = _activeCompositor;
    var effects = _compositor == null ? new Godot.Collections.Array<CompositorEffect>()
      : new Godot.Collections.Array<CompositorEffect>(_compositor.CompositorEffects);
    effects.Add(_effect);
    _camera.Compositor.CompositorEffects = effects;
    SetProcess(false);
  }

  public void ShowTarget(Node3D target, IReadOnlyList<MeshInstance3D> meshes) {
    if (_fallback != null) { _fallback.ShowTarget(target, meshes); return; }
    HideTarget(_target);
    if (_effect == null) return;
    _target = target;
    foreach (var mesh in meshes) {
      if (!GodotObject.IsInstanceValid(mesh) || mesh.Mesh == null) continue;
      if (mesh.MaterialOverride != null) {
        var original = mesh.MaterialOverride;
        if (StencilMaterial(original) is not { } stencil) continue;
        _overrides.Add((mesh, -1, original));
        mesh.MaterialOverride = stencil;
      }
      else {
        for (var surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++) {
          var original = mesh.GetSurfaceOverrideMaterial(surface);
          if (StencilMaterial(mesh.GetActiveMaterial(surface)) is not { } stencil) continue;
          _overrides.Add((mesh, surface, original));
          mesh.SetSurfaceOverrideMaterial(surface, stencil);
        }
      }
    }
    _effect.Enabled = _overrides.Count > 0;
    SetProcess(true);
  }

  private BaseMaterial3D StencilMaterial(Material original) {
    if (original is not BaseMaterial3D material) {
      GD.PushWarning($"Stencil hover does not support material {original?.ResourcePath}.");
      return null;
    }
    if (_materials.TryGetValue(original, out var cached)) return cached;
    var stencil = (BaseMaterial3D)material.Duplicate();
    stencil.StencilMode = BaseMaterial3D.StencilModeEnum.Custom;
    stencil.StencilFlags = (int)BaseMaterial3D.StencilFlagsEnum.Write;
    stencil.StencilReference = 1;
    _materials.Add(original, stencil);
    return stencil;
  }

  public void HideTarget(Node3D target) {
    if (GodotObject.IsInstanceValid(_fallback)) { _fallback.HideTarget(target); return; }
    if (_target != target) return;
    if (_effect != null) _effect.Enabled = false;
    foreach (var (mesh, surface, original) in _overrides) {
      if (!GodotObject.IsInstanceValid(mesh)) continue;
      if (surface < 0) mesh.MaterialOverride = original;
      else mesh.SetSurfaceOverrideMaterial(surface, original);
    }
    _overrides.Clear();
    _target = null;
    SetProcess(false);
  }

  public override void _Process(double delta) {
    if (!GodotObject.IsInstanceValid(_target) || !_target.IsInsideTree() || !_target.IsVisibleInTree())
      HideTarget(_target);
  }

  public override void _ExitTree() {
    HideTarget(_target);
    if (GodotObject.IsInstanceValid(_fallback)) _fallback.QueueFree();
    if (_activeCompositor != null && GodotObject.IsInstanceValid(_camera) && _camera.Compositor == _activeCompositor)
      _camera.Compositor = _compositor;
    if (_activeCompositor != null) {
      _activeCompositor.CompositorEffects = new Godot.Collections.Array<CompositorEffect>();
      _activeCompositor.Dispose();
    }
    if (_effect != null) {
      _effect.Call("release_resources");
      _effect.Dispose();
    }
    _materials.Clear();
  }
}
