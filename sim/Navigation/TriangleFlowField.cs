using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Deterministic.Navigation;

namespace Meesles.Avalon.Sim.Navigation;

// Per-triangle route toward one goal.
public class TriangleFlowField {
  public const int AtGoal = -1;
  public const int Unreachable = -2;
  public readonly int[] NextTriangle;

  private readonly FPVector2[] _exitDirection;
  private readonly bool[] _exitKnown;
  private readonly FPNavMesh _navMesh;

  internal TriangleFlowField(FPNavMesh navMesh, int[] nextTriangle) {
    _navMesh = navMesh;
    NextTriangle = nextTriangle;
    _exitDirection = new FPVector2[nextTriangle.Length];
    _exitKnown = new bool[nextTriangle.Length];
  }

  // Lazily cached direction to the next portal midpoint.
  public FPVector2 GetExitDirection(int triangle) {
    if (_exitKnown[triangle])
      return _exitDirection[triangle];

    var direction = FPVector2.Zero;
    var nextTriangle = NextTriangle[triangle];

    if (nextTriangle >= 0) {
      ref readonly var sourceTriangle = ref _navMesh.Triangles[triangle];
      var toPortal = GetPortalMidpoint(triangle, nextTriangle) - sourceTriangle.centerXZ;
      var mag = toPortal.magnitude;
      if (mag > FP64.Zero)
        direction = toPortal / mag;
    }

    _exitDirection[triangle] = direction;
    _exitKnown[triangle] = true;
    return direction;
  }

  private FPVector2 GetPortalMidpoint(int fromTri, int toTri) {
    ref readonly var sourceTriangle = ref _navMesh.Triangles[fromTri];

    for (var edge = 0; edge < 3; edge++)
      if (sourceTriangle.GetNeighbor(edge) == toTri) {
        sourceTriangle.GetEdgeVertices(edge, out var va, out var vb);
        var a = _navMesh.Vertices[va];
        var b = _navMesh.Vertices[vb];
        return new FPVector2(
          (a.x + b.x) * FP64.Half,
          (a.z + b.z) * FP64.Half);
      }

    return sourceTriangle.centerXZ;
  }
}
