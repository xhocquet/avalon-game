using Godot;

namespace Meesles.Avalon.Client.Scripts.View;

[Tool]
public partial class CrystalBulletEffect : Node3D {
  private const float HalfLength = 0.3f;
  private const float BaseRadius = 0.12f;
  private const int Sides = 5;

  private static ArrayMesh _crystalMesh;

  public override void _Ready() => EnsureMesh();

  public void Configure(float radius) {
    EnsureMesh();
    Scale = Vector3.One * Mathf.Max(radius / BaseRadius, 0.01f);
    Show();
  }

  public void ResetVisual() {
    Hide();
    GlobalPosition = Vector3.Zero;
    GlobalRotation = Vector3.Zero;
    Scale = Vector3.One;
  }

  private void EnsureMesh() {
    var mesh = GetNodeOrNull<MeshInstance3D>("CrystalMesh");
    if (mesh != null)
      mesh.Mesh = _crystalMesh ??= CreateCrystalMesh();
  }

  private static ArrayMesh CreateCrystalMesh() {
    var surface = new SurfaceTool();
    surface.Begin(Mesh.PrimitiveType.Triangles);

    var forwardTip = new Vector3(0f, 0f, HalfLength);
    var rearTip = new Vector3(0f, 0f, -HalfLength);
    for (var i = 0; i < Sides; i++) {
      var a = RingVertex(i);
      var b = RingVertex((i + 1) % Sides);
      AddFace(surface, forwardTip, a, b);
      AddFace(surface, rearTip, b, a);
    }

    return surface.Commit();
  }

  private static Vector3 RingVertex(int index) {
    var angle = Mathf.Tau * index / Sides;
    return new Vector3(Mathf.Cos(angle) * BaseRadius, Mathf.Sin(angle) * BaseRadius, 0f);
  }

  private static void AddFace(SurfaceTool surface, Vector3 a, Vector3 b, Vector3 c) {
    var normal = (b - a).Cross(c - a).Normalized();
    var center = (a + b + c) / 3f;
    if (normal.Dot(center) < 0f) {
      (b, c) = (c, b);
      normal = -normal;
    }

    AddVertex(surface, a, normal, Colors.Red);
    AddVertex(surface, b, normal, Colors.Green);
    AddVertex(surface, c, normal, Colors.Blue);
  }

  private static void AddVertex(SurfaceTool surface, Vector3 position, Vector3 normal, Color color) {
    surface.SetNormal(normal);
    surface.SetColor(color);
    surface.AddVertex(position);
  }
}
