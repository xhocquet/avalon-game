using Godot;

namespace Meesles.Avalon;

public partial class DebugAttackLine : MeshInstance3D {
  private const float Duration = 0.3f;
  private const float ModelCenterY = 1.0f;

  private float _elapsed;

  public static DebugAttackLine Create(Vector3 from, Vector3 to) {
    var node = new DebugAttackLine();

    from.Y += ModelCenterY;
    to.Y += ModelCenterY;

    var mesh = new ImmediateMesh();
    mesh.SurfaceBegin(Mesh.PrimitiveType.Lines);
    mesh.SurfaceSetColor(Colors.Red);
    mesh.SurfaceAddVertex(from);
    mesh.SurfaceSetColor(Colors.Red);
    mesh.SurfaceAddVertex(to);
    mesh.SurfaceEnd();

    var mat = new StandardMaterial3D {
      AlbedoColor = Colors.Red,
      ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
      VertexColorUseAsAlbedo = true,
      NoDepthTest = true
    };
    mesh.SurfaceSetMaterial(0, mat);

    node.Mesh = mesh;
    return node;
  }

  public override void _Process(double delta) {
    _elapsed += (float)delta;
    if (_elapsed >= Duration) {
      QueueFree();
      return;
    }

    var alpha = 1f - _elapsed / Duration;
    if (Mesh is ImmediateMesh im && im.GetSurfaceCount() > 0) {
      var mat = im.SurfaceGetMaterial(0) as StandardMaterial3D;
      if (mat != null) {
        mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        mat.AlbedoColor = new Color(1f, 0f, 0f, alpha);
      }
    }
  }
}
