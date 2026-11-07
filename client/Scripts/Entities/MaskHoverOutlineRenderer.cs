using System.Collections.Generic;
using Godot;

namespace Meesles.Avalon.Client.Scripts.Entities;

public partial class MaskHoverOutlineRenderer : Node {
  private const uint MaskLayer = 1u << 18;
  private const uint WorldLayer = 1u << 2;
  private readonly List<MeshInstance3D> _proxies = new();
  private Camera3D _sourceCamera;
  private Camera3D _maskCamera;
  private SubViewport _maskViewport;
  private MeshInstance3D _screenQuad;
  private ShaderMaterial _outlineMaterial;
  private ShaderMaterial _maskMaterial;
  private Node3D _target;
  private IReadOnlyList<MeshInstance3D> _sources;

  public override void _Ready() {
    ProcessPriority = 100; // After camera and entity-view updates.
    _sourceCamera = GetParent<Camera3D>();
    _maskMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://Assets/FX/Shaders/HoverMask.gdshader") };
    _outlineMaterial = (ShaderMaterial)GD.Load<ShaderMaterial>("res://Assets/FX/Shaders/HoverOutline.tres").Duplicate();
    _maskViewport = new SubViewport {
      Name = "SilhouetteMask",
      Size = new Vector2I(2, 2),
      UseHdr2D = true,
      GuiDisableInput = true,
      RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled,
      Msaa3D = Viewport.Msaa.Disabled,
      ScreenSpaceAA = Viewport.ScreenSpaceAAEnum.Disabled
    };
    AddChild(_maskViewport);
    _maskCamera = new Camera3D {
      Name = "MaskCamera",
      CullMask = MaskLayer,
      Current = true,
      Environment = new Environment {
        BackgroundMode = Environment.BGMode.Color,
        BackgroundColor = Colors.Black,
        TonemapMode = Environment.ToneMapper.Linear,
        AmbientLightEnergy = 0f,
        ReflectedLightSource = Environment.ReflectionSource.Disabled
      }
    };
    _maskViewport.AddChild(_maskCamera);
    _outlineMaterial.SetShaderParameter("mask_texture", _maskViewport.GetTexture());
    _screenQuad = new MeshInstance3D {
      Name = "OutlineComposite",
      Mesh = new QuadMesh { Size = new Vector2(2, 2) },
      MaterialOverride = _outlineMaterial,
      Layers = WorldLayer,
      CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
      GIMode = GeometryInstance3D.GIModeEnum.Disabled,
      ExtraCullMargin = 10000f,
      IgnoreOcclusionCulling = true,
      Visible = false
    };
    AddChild(_screenQuad);
    SetProcess(false);
  }

  public void ShowTarget(Node3D target, IReadOnlyList<MeshInstance3D> meshes) {
    _target = target;
    _sources = meshes;
    while (_proxies.Count < meshes.Count) {
      var proxy = new MeshInstance3D {
        Name = $"MaskMesh{_proxies.Count}",
        Layers = MaskLayer,
        MaterialOverride = _maskMaterial,
        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        GIMode = GeometryInstance3D.GIModeEnum.Disabled,
        TopLevel = true
      };
      AddChild(proxy);
      _proxies.Add(proxy);
    }
    for (var i = 0; i < _proxies.Count; i++) {
      var proxy = _proxies[i];
      proxy.Visible = false;
      proxy.Mesh = i < meshes.Count ? meshes[i].Mesh : null;
      proxy.Skin = i < meshes.Count ? meshes[i].Skin : null;
      var skeleton = i < meshes.Count ? FindSkeleton(meshes[i]) : null;
      proxy.Skeleton = skeleton?.GetPath() ?? new NodePath("");
    }
    SetProcess(true);
    UpdateMask();
  }

  public void HideTarget(Node3D target) {
    if (_target != target) return;
    _target = null;
    _sources = null;
    _screenQuad.Hide();
    _maskViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
    foreach (var proxy in _proxies) proxy.Hide();
    SetProcess(false);
  }

  public override void _Process(double delta) {
    if (!GodotObject.IsInstanceValid(_target) || !_target.IsInsideTree() || !_target.IsVisibleInTree()) {
      HideTarget(_target);
      return;
    }
    UpdateMask();
  }

  private void UpdateMask() {
    var viewportSize = _sourceCamera.GetViewport().GetVisibleRect().Size;
    if (viewportSize.X < 2f || viewportSize.Y < 2f) {
      _screenQuad.Hide();
      _maskViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
      return;
    }
    var maskSize = new Vector2I(Mathf.Max(2, Mathf.CeilToInt(viewportSize.X * 0.5f)),
      Mathf.Max(2, Mathf.CeilToInt(viewportSize.Y * 0.5f)));
    if (_maskViewport.Size != maskSize) _maskViewport.Size = maskSize;
    _maskCamera.GlobalTransform = _sourceCamera.GlobalTransform;
    _maskCamera.Projection = _sourceCamera.Projection;
    _maskCamera.KeepAspect = _sourceCamera.KeepAspect;
    _maskCamera.Fov = _sourceCamera.Fov;
    _maskCamera.Size = _sourceCamera.Size;
    _maskCamera.Near = _sourceCamera.Near;
    _maskCamera.Far = _sourceCamera.Far;
    _maskCamera.HOffset = _sourceCamera.HOffset;
    _maskCamera.VOffset = _sourceCamera.VOffset;
    _maskCamera.FrustumOffset = _sourceCamera.FrustumOffset;

    var bounds = new Rect2();
    var haveBounds = false;
    var crossesCamera = false;
    var maxDepth = 0f;
    for (var i = 0; i < _sources.Count; i++) {
      var source = _sources[i];
      var proxy = _proxies[i];
      proxy.Visible = GodotObject.IsInstanceValid(source) && source.IsVisibleInTree();
      if (!proxy.Visible) continue;
      proxy.GlobalTransform = source.GlobalTransform;
      for (var shape = 0; shape < source.GetBlendShapeCount(); shape++)
        proxy.SetBlendShapeValue(shape, source.GetBlendShapeValue(shape));
      var box = source.GetAabb();
      for (var corner = 0; corner < 8; corner++) {
        var world = source.GlobalTransform * box.GetEndpoint(corner);
        maxDepth = Mathf.Max(maxDepth, _sourceCamera.GlobalPosition.DistanceTo(world));
        if (_sourceCamera.IsPositionBehind(world)) { crossesCamera = true; continue; }
        var screen = _sourceCamera.UnprojectPosition(world);
        bounds = haveBounds ? bounds.Expand(screen) : new Rect2(screen, Vector2.Zero);
        haveBounds = true;
      }
    }
    var depthRange = Mathf.Max(32f, maxDepth + 8f);
    _maskMaterial.SetShaderParameter("depth_range", depthRange);
    _outlineMaterial.SetShaderParameter("depth_range", depthRange);
    var visibleRect = new Rect2(Vector2.Zero, viewportSize);
    bounds = crossesCamera ? visibleRect : bounds.Grow(24f).Intersection(visibleRect);
    _outlineMaterial.SetShaderParameter("screen_bounds", new Vector4(bounds.Position.X / viewportSize.X,
      bounds.Position.Y / viewportSize.Y, bounds.End.X / viewportSize.X, bounds.End.Y / viewportSize.Y));
    _screenQuad.Visible = haveBounds && bounds.HasArea();
    _maskViewport.RenderTargetUpdateMode = _screenQuad.Visible
      ? SubViewport.UpdateMode.Always : SubViewport.UpdateMode.Disabled;
  }

  private static Skeleton3D FindSkeleton(MeshInstance3D mesh) {
    if (!mesh.Skeleton.IsEmpty && mesh.GetNodeOrNull<Skeleton3D>(mesh.Skeleton) is { } skeleton) return skeleton;
    for (var parent = mesh.GetParent(); parent != null; parent = parent.GetParent())
      if (parent is Skeleton3D ancestor) return ancestor;
    return null;
  }
}
