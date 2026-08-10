using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Deterministic.Navigation;

namespace Meesles.Avalon.Sim.Navigation;

// One goal's routing table: per triangle, the neighbour to cross into on the way there. Built by
// FlowFieldBuilder and immutable afterwards.
public class TriangleFlowField {
  public const int AtGoal = -1;
  public const int Unreachable = -2;
  public readonly int[] NextTriangle;

  // Restore if we need dist-to-goal (e.g. ETA/threat maps) or to identify the goal triangle
  // public readonly FP64[] Cost;
  // public readonly int GoalTriangleIndex;

  private readonly FPVector2[] _exitDirection;
  private readonly bool[] _exitKnown;
  private readonly FPNavMesh _navMesh;

  internal TriangleFlowField(FPNavMesh navMesh, int[] nextTriangle) {
    _navMesh = navMesh;
    NextTriangle = nextTriangle;
    _exitDirection = new FPVector2[nextTriangle.Length];
    _exitKnown = new bool[nextTriangle.Length];
  }

  // Direction from the triangle's centre toward the midpoint of the portal leading to the next
  // triangle in the path. Each one costs a fixed-point sqrt and divide, and a field is only ever
  // asked about the handful of triangles units are standing in, so they're filled in on demand
  // rather than all at build time. The value is a pure function of the navmesh and the route, so
  // when it gets computed is invisible to the simulation.
  public FPVector2 GetExitDirection(int triangle) {
    if (_exitKnown[triangle])
      return _exitDirection[triangle];

    var direction = FPVector2.Zero;
    var nextTri = NextTriangle[triangle];

    if (nextTri >= 0) {
      ref var tri = ref _navMesh.Triangles[triangle];
      var toPortal = GetPortalMidpoint(triangle, nextTri) - tri.centerXZ;
      var mag = toPortal.magnitude;
      if (mag > FP64.Zero)
        direction = toPortal / mag;
    }

    _exitDirection[triangle] = direction;
    _exitKnown[triangle] = true;
    return direction;
  }

  private FPVector2 GetPortalMidpoint(int fromTri, int toTri) {
    ref var tri = ref _navMesh.Triangles[fromTri];

    for (var edge = 0; edge < 3; edge++)
      if (tri.GetNeighbor(edge) == toTri) {
        tri.GetEdgeVertices(edge, out var va, out var vb);
        var a = _navMesh.Vertices[va];
        var b = _navMesh.Vertices[vb];
        return new FPVector2(
          (a.x + b.x) * FP64.Half,
          (a.z + b.z) * FP64.Half);
      }

    return tri.centerXZ;
  }
}
