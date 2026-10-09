using Godot;

namespace Meesles.Avalon.Client.Scripts.View;

public partial class DebugAttackLine : MeshInstance3D {
  private const float Duration = 0.3f;
  private const float ModelCenterY = 1.0f;

  private float _elapsed;

  public static DebugAttackLine Create(Vector3 from, Vector3 to) {
    return Create(from, to, CreateMesh());
  }

  public static DebugAttackLine Create(Vector3 from, Vector3 to, Mesh mesh) {
    var node = new DebugAttackLine {
      Mesh = mesh,
      CastShadow = ShadowCastingSetting.Off
    };

    from.Y += ModelCenterY;
    to.Y += ModelCenterY;

    var offset = to - from;
    node.Position = from;
    if (offset.LengthSquared() > 0f)
      node.Basis = new Basis(new Quaternion(Vector3.Forward, offset.Normalized()))
                   * Basis.FromScale(new Vector3(1f, 1f, offset.Length()));
    else
      node.Visible = false;

    return node;
  }

  public static Mesh CreateMesh() {
    var mesh = new ImmediateMesh();
    mesh.SurfaceBegin(Mesh.PrimitiveType.Lines);
    mesh.SurfaceSetColor(Colors.Red);
    mesh.SurfaceAddVertex(Vector3.Zero);
    mesh.SurfaceSetColor(Colors.Red);
    mesh.SurfaceAddVertex(Vector3.Forward);
    mesh.SurfaceEnd();

    var mat = new StandardMaterial3D {
      AlbedoColor = Colors.Red,
      ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
      VertexColorUseAsAlbedo = true,
      Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
      NoDepthTest = true
    };
    mesh.SurfaceSetMaterial(0, mat);

    return mesh;
  }

  public override void _Process(double delta) {
    _elapsed += (float)delta;
    if (_elapsed >= Duration) {
      QueueFree();
      return;
    }

    Transparency = _elapsed / Duration;
  }
}
