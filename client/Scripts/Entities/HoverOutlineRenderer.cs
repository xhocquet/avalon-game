using System.Collections.Generic;
using Godot;

namespace Meesles.Avalon.Client.Scripts.Entities;

public partial class HoverOutlineRenderer : Node {
  private sealed class Target {
    public bool Selected;
    public bool Hovered;
    public readonly List<(MeshInstance3D Mesh, int Surface, Material Original)> Overrides = new();
  }

  private readonly Dictionary<(Material Material, int Reference), BaseMaterial3D> _materials = new();
  private readonly HashSet<Material> _unsupportedMaterials = new();
  private readonly Dictionary<Node3D, Target> _targets = new();
  private readonly List<Node3D> _expired = new();
  private Camera3D _camera;
  private Compositor _compositor;
  private Compositor _activeCompositor;
  private CompositorEffect _effect;
  private MaskHoverOutlineRenderer _fallback;

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
    _effect.Set("team_colors", new Godot.Collections.Array<Color> {
      TeamPalette.Default.ForTeam(-1), TeamPalette.Default.ForTeam(1), TeamPalette.Default.ForTeam(2)
    });
    _compositor = _camera.Compositor;
    _activeCompositor = new Compositor();
    var effects = _compositor == null ? new Godot.Collections.Array<CompositorEffect>()
      : new Godot.Collections.Array<CompositorEffect>(_compositor.CompositorEffects);
    effects.Add(_effect);
    _activeCompositor.CompositorEffects = effects;
    _camera.Compositor = _activeCompositor;
    SetProcess(false);
  }

  public void SetTarget(Node3D target, IReadOnlyList<MeshInstance3D> meshes, int teamId, bool selected, bool hovered) {
    if (_fallback != null) {
      if (hovered) {
        _fallback.SetColor(TeamPalette.Default.ForTeam(teamId));
        _fallback.ShowTarget(target, meshes);
      }
      else _fallback.HideTarget(target);
      return;
    }
    if (_effect == null) return;
    if (_targets.Remove(target, out var previous)) Restore(previous);
    if (selected || hovered) {
      var state = new Target { Selected = selected, Hovered = hovered };
      var team = teamId is 1 or 2 ? teamId : 0;
      var reference = team | (selected ? 4 : 0) | (hovered ? 8 : 0);
      foreach (var mesh in meshes) {
        if (!GodotObject.IsInstanceValid(mesh) || mesh.Mesh == null) continue;
        if (mesh.MaterialOverride != null) {
          var original = mesh.MaterialOverride;
          if (StencilMaterial(original, reference, mesh, -1) is not { } stencil) continue;
          state.Overrides.Add((mesh, -1, original));
          mesh.MaterialOverride = stencil;
        }
        else {
          for (var surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++) {
            var original = mesh.GetSurfaceOverrideMaterial(surface);
            if (StencilMaterial(mesh.GetActiveMaterial(surface), reference, mesh, surface) is not { } stencil) continue;
            state.Overrides.Add((mesh, surface, original));
            mesh.SetSurfaceOverrideMaterial(surface, stencil);
          }
        }
      }
      _targets.Add(target, state);
    }
    UpdateLayers();
  }

  private BaseMaterial3D StencilMaterial(Material original, int reference, MeshInstance3D mesh, int surface) {
    if (original is not BaseMaterial3D material) {
      if (_unsupportedMaterials.Add(original))
        GD.PushWarning($"Stencil outline does not support {original?.GetClass() ?? "default material"} on {mesh.GetPath()} (surface {surface}).");
      return null;
    }
    var key = (original, reference);
    if (_materials.TryGetValue(key, out var cached)) return cached;
    var stencil = (BaseMaterial3D)material.Duplicate();
    stencil.StencilMode = BaseMaterial3D.StencilModeEnum.Custom;
    stencil.StencilFlags = (int)BaseMaterial3D.StencilFlagsEnum.Write;
    stencil.StencilReference = reference;
    _materials.Add(key, stencil);
    return stencil;
  }

  private static void Restore(Target target) {
    foreach (var (mesh, surface, original) in target.Overrides) {
      if (!GodotObject.IsInstanceValid(mesh)) continue;
      if (surface < 0) mesh.MaterialOverride = original;
      else mesh.SetSurfaceOverrideMaterial(surface, original);
    }
  }

  private void UpdateLayers() {
    var selected = false;
    var hovered = false;
    foreach (var target in _targets.Values) {
      if (target.Overrides.Count == 0) continue;
      selected |= target.Selected;
      hovered |= target.Hovered;
    }
    _effect.Set("selection_active", selected);
    _effect.Set("hover_active", hovered);
    _effect.Enabled = selected || hovered;
    SetProcess(_targets.Count > 0);
  }

  public void HideTarget(Node3D target) {
    if (target == null) return;
    if (GodotObject.IsInstanceValid(_fallback)) _fallback.HideTarget(target);
    if (_targets.Remove(target, out var state)) Restore(state);
    if (_effect != null) UpdateLayers();
  }

  public override void _Process(double delta) {
    _expired.Clear();
    foreach (var target in _targets.Keys)
      if (!GodotObject.IsInstanceValid(target) || !target.IsInsideTree() || !target.IsVisibleInTree()) _expired.Add(target);
    foreach (var target in _expired) HideTarget(target);
  }

  public override void _ExitTree() {
    foreach (var state in _targets.Values) Restore(state);
    _targets.Clear();
    if (GodotObject.IsInstanceValid(_fallback)) _fallback.QueueFree();
    if (_activeCompositor != null && GodotObject.IsInstanceValid(_camera) && _camera.Compositor == _activeCompositor)
      _camera.Compositor = _compositor;
    if (_activeCompositor != null) {
      _activeCompositor.CompositorEffects = new Godot.Collections.Array<CompositorEffect>();
      _activeCompositor.Dispose();
    }
    if (_effect != null) {
      _effect.Enabled = false;
      _effect.Call("release_resources");
      _effect.Dispose();
    }
    _materials.Clear();
    _unsupportedMaterials.Clear();
  }
}
