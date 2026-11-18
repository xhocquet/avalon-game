using System.Collections.Generic;
using Godot;
using Meesles.Avalon.Client.Scripts.View;

namespace Meesles.Avalon.Client;

[Tool]
public partial class MinimapView : Control {
  [Export] public Texture2D PreviewTexture { get; set; } = GD.Load<Texture2D>("res://Assets/UI/Minimap/avalon.png");
  private MinimapBounds _bounds;
  private readonly Dictionary<Node3D, TextureRect> _icons = new();
  private readonly List<Node3D> _removed = new();
  private Line2D _viewportFootprint;
  private MinimapPath _path;
  private ColorRect _fog;
  private ShaderMaterial _fogMaterial;

  public override void _Ready() {
    Resized += QueueRedraw;
    _viewportFootprint = GetNode<Line2D>("ViewportFootprint");
    _path = GetNode<MinimapPath>("PlayerPath");
    if (Engine.IsEditorHint()) return;
    _viewportFootprint.Visible = false;
    if (DisplayServer.GetName() == "headless") {
      SetProcess(false);
      return;
    }
    ProcessPriority = 100;
    _fogMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://Assets/FX/Shaders/MinimapFog.gdshader") };
    _fog = new ColorRect { MouseFilter = MouseFilterEnum.Ignore, Material = _fogMaterial, ZIndex = 10, Visible = false };
    AddChild(_fog);
  }

  public override void _Process(double delta) {
    if (Engine.IsEditorHint()) return;
    if (!GodotObject.IsInstanceValid(_bounds)) {
      _bounds = GetTree().GetFirstNodeInGroup("minimap_bounds") as MinimapBounds;
      if (_bounds == null && GetTree().CurrentScene?.FindChild("GroundMesh", true, false) is MeshInstance3D ground) {
        _bounds = new MinimapBounds { Ground = ground };
        ground.AddChild(_bounds);
      }
      QueueRedraw();
    }
    if (_bounds?.Ground == null) {
      _viewportFootprint.Visible = false;
      return;
    }
    foreach (var entity in GetTree().GetNodesInGroup("minimap_entities")) {
      var marker = entity.GetNodeOrNull<Node3D>("MinimapMarker");
      if (marker == null) continue;
      if (!_icons.TryGetValue(marker, out var icon)) {
        icon = new TextureRect { MouseFilter = MouseFilterEnum.Ignore, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, ZIndex = 12 };
        if (marker is MinimapHeroMarker hero) {
          icon.Texture = hero.Portrait;
          icon.Material = new ShaderMaterial { Shader = GD.Load<Shader>("res://Assets/FX/Shaders/MinimapPortrait.gdshader") };
          icon.ZIndex = 13;
        }
        else if (marker is Sprite3D sprite) icon.Texture = sprite.Texture;
        AddChild(icon);
        _icons.Add(marker, icon);
      }
      var uv = _bounds.WorldToUv(marker.GlobalPosition);
      icon.Visible = marker.IsVisibleInTree() && new Rect2(Vector2.Zero, Vector2.One).HasPoint(uv);
      if (!icon.Visible) continue;
      Vector2 worldSize;
      if (marker is MinimapHeroMarker heroMarker) {
        worldSize = ((QuadMesh)heroMarker.Mesh).Size;
        ((ShaderMaterial)icon.Material).SetShaderParameter("team_color", heroMarker.TeamColor);
        icon.Texture = heroMarker.Portrait;
      }
      else {
        var sprite = (Sprite3D)marker;
        var scale = sprite.GlobalBasis.Scale.Abs();
        worldSize = sprite.Texture.GetSize() * sprite.PixelSize * new Vector2(scale.X, scale.Y);
        icon.Modulate = sprite.Modulate;
      }
      var groundSize = _bounds.Ground.GetAabb().Size * _bounds.Ground.GlobalBasis.Scale.Abs();
      icon.Size = worldSize / new Vector2(groundSize.X, groundSize.Z) * Size;
      icon.Position = uv * Size - icon.Size / 2;
    }
    _removed.Clear();
    foreach (var pair in _icons) {
      if (GodotObject.IsInstanceValid(pair.Key) && pair.Key.IsInsideTree()) continue;
      pair.Value.QueueFree();
      _removed.Add(pair.Key);
    }
    foreach (var marker in _removed) _icons.Remove(marker);
    _path.Bounds = _bounds;
    _fog.Size = Size;
    UpdateViewportFootprint();
  }

  private void UpdateViewportFootprint() {
    var viewport = GetViewport();
    var camera = viewport.GetCamera3D();
    _viewportFootprint.Visible = false;
    if (camera == null) return;
    var screenSize = viewport.GetVisibleRect().Size;
    var planeOrigin = _bounds.UvToWorld(Vector2.Zero);
    var normal = _bounds.Ground.GlobalBasis.Y.Normalized();
    for (var i = 0; i < 4; i++) {
      var screen = new Vector2(i == 1 || i == 2 ? screenSize.X : 0, i >= 2 ? screenSize.Y : 0);
      var origin = camera.ProjectRayOrigin(screen);
      var direction = camera.ProjectRayNormal(screen);
      var denominator = direction.Dot(normal);
      if (Mathf.Abs(denominator) < 0.0001f) return;
      var distance = (planeOrigin - origin).Dot(normal) / denominator;
      if (distance < 0) return;
      var point = _bounds.WorldToUv(origin + direction * distance) * Size;
      _viewportFootprint.SetPointPosition(i, point);
    }
    _viewportFootprint.SetPointPosition(4, _viewportFootprint.GetPointPosition(0));
    _viewportFootprint.Visible = true;
  }

  public override void _Draw() {
    var texture = Engine.IsEditorHint() ? PreviewTexture : _bounds?.Texture;
    var rect = new Rect2(Vector2.Zero, Size);
    if (texture != null) DrawTextureRect(texture, rect, false);
    else DrawRect(rect, new Color(0.12f, 0.17f, 0.14f));
  }

  public void BindNavigation(xpTURN.Klotho.Deterministic.Navigation.FPNavMesh mesh) => _path.BindNavigation(mesh);

  public void SyncPath(xpTURN.Klotho.ECS.Frame frame, int? playerId) {
    if (DisplayServer.GetName() != "headless") _path.Sync(frame, playerId);
  }

  public bool TryGetWorldPosition(Vector2 screenPosition, bool clamp, out Vector3 position) {
    position = default;
    if (!IsVisibleInTree() || !GodotObject.IsInstanceValid(_bounds) || _bounds.Ground == null ||
        Size.X <= 0 || Size.Y <= 0) return false;
    var local = GetGlobalTransformWithCanvas().AffineInverse() * screenPosition;
    if (!clamp && !new Rect2(Vector2.Zero, Size).HasPoint(local)) return false;
    var uv = (local / Size).Clamp(Vector2.Zero, Vector2.One);
    position = _bounds.UvToWorld(uv);
    return true;
  }

  public void SyncVision(VisionRenderer vision) {
    if (_fog == null || _bounds?.Ground == null) return;
    _fog.Visible = vision.Visible && vision.MinimapTexture != null;
    if (!_fog.Visible) return;
    _fogMaterial.SetShaderParameter("vision_texture", vision.MinimapTexture);
    _fogMaterial.SetShaderParameter("vision_origin", vision.MinimapOrigin);
    _fogMaterial.SetShaderParameter("vision_size", vision.MinimapSize);
    var origin = _bounds.UvToWorld(Vector2.Zero);
    var right = _bounds.UvToWorld(Vector2.Right) - origin;
    var down = _bounds.UvToWorld(Vector2.Down) - origin;
    _fogMaterial.SetShaderParameter("map_origin", new Vector2(origin.X, origin.Z));
    _fogMaterial.SetShaderParameter("map_right", new Vector2(right.X, right.Z));
    _fogMaterial.SetShaderParameter("map_down", new Vector2(down.X, down.Z));
  }
}
