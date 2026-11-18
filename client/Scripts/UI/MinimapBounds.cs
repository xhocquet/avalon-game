using Godot;

namespace Meesles.Avalon.Client;

[Tool]
public partial class MinimapBounds : Node3D {
  [Export] public MeshInstance3D Ground { get; set; }
  [Export] public Texture2D Texture { get; set; }

  public override void _EnterTree() => AddToGroup("minimap_bounds");

  public override void _Ready() {
    if (!Engine.IsEditorHint() || Ground == null) return;
    var mesh = new ImmediateMesh();
    var material = new StandardMaterial3D {
      ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
      AlbedoColor = new Color(1, 0.75f, 0.1f),
      NoDepthTest = true
    };
    mesh.SurfaceBegin(Mesh.PrimitiveType.Lines, material);
    var corners = new[] { Vector2.Zero, Vector2.Right, Vector2.One, Vector2.Down };
    for (var i = 0; i < corners.Length; i++) {
      mesh.SurfaceAddVertex(ToLocal(UvToWorld(corners[i])) + Vector3.Up * 0.1f);
      mesh.SurfaceAddVertex(ToLocal(UvToWorld(corners[(i + 1) % corners.Length])) + Vector3.Up * 0.1f);
    }
    mesh.SurfaceEnd();
    AddChild(new MeshInstance3D { Name = "BoundsPreview", Mesh = mesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
  }

  public Vector2 WorldToUv(Vector3 position) {
    var local = Ground.ToLocal(position);
    var bounds = Ground.GetAabb();
    return new Vector2((local.X - bounds.Position.X) / bounds.Size.X,
      (local.Z - bounds.Position.Z) / bounds.Size.Z);
  }

  public Vector3 UvToWorld(Vector2 uv) {
    var bounds = Ground.GetAabb();
    return Ground.ToGlobal(bounds.Position + new Vector3(uv.X * bounds.Size.X, 0, uv.Y * bounds.Size.Z));
  }
}
