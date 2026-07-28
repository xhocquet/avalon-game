using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Deterministic.Navigation;

namespace Meesles.Avalon.Sim.Navigation;

public class TriangleFlowField {
  public const int AtGoal = -1;
  public const int Unreachable = -2;
  public readonly FP64[] Cost;
  public readonly FPVector2[] ExitDirection;

  public readonly int GoalTriangleIndex;
  public readonly int[] NextTriangle;

  private TriangleFlowField(int goalTriangleIndex, int[] nextTriangle, FPVector2[] exitDirection, FP64[] cost) {
    GoalTriangleIndex = goalTriangleIndex;
    NextTriangle = nextTriangle;
    ExitDirection = exitDirection;
    Cost = cost;
  }

  public static TriangleFlowField Compute(FPNavMesh navMesh, int goalTriIndex) {
    var triCount = navMesh.Triangles.Length;
    var next = new int[triCount];
    var exitDir = new FPVector2[triCount];
    var cost = new FP64[triCount];

    for (var i = 0; i < triCount; i++) {
      next[i] = Unreachable;
      cost[i] = FP64.MaxValue;
    }

    next[goalTriIndex] = AtGoal;
    cost[goalTriIndex] = FP64.Zero;

    // Dijkstra BFS from goal outward using a simple priority queue.
    // With <1000 triangles, an array-scan "queue" is fast enough and avoids allocations.
    var open = new bool[triCount];
    open[goalTriIndex] = true;
    var openCount = 1;

    while (openCount > 0) {
      // Find lowest-cost open node
      var current = -1;
      var bestCost = FP64.MaxValue;
      for (var i = 0; i < triCount; i++)
        if (open[i] && cost[i] < bestCost) {
          bestCost = cost[i];
          current = i;
        }

      if (current < 0)
        break;

      open[current] = false;
      openCount--;

      ref var currentTri = ref navMesh.Triangles[current];

      for (var edge = 0; edge < 3; edge++) {
        var neighborIdx = currentTri.GetNeighbor(edge);
        if (neighborIdx < 0)
          continue;

        ref var neighborTri = ref navMesh.Triangles[neighborIdx];
        if (neighborTri.isBlocked)
          continue;

        var delta = neighborTri.centerXZ - currentTri.centerXZ;
        var edgeCost = delta.magnitude * neighborTri.costMultiplier;
        var newCost = cost[current] + edgeCost;

        if (newCost < cost[neighborIdx]) {
          cost[neighborIdx] = newCost;
          next[neighborIdx] = current;

          if (!open[neighborIdx]) {
            open[neighborIdx] = true;
            openCount++;
          }
        }
      }
    }

    // Compute exit directions: from each triangle's center toward the portal midpoint
    // leading to the next triangle in the path.
    for (var i = 0; i < triCount; i++) {
      if (next[i] < 0) {
        exitDir[i] = FPVector2.Zero;
        continue;
      }

      var nextTri = next[i];
      var portalMid = GetPortalMidpoint(navMesh, i, nextTri);
      var dir = portalMid - navMesh.Triangles[i].centerXZ;
      var mag = dir.magnitude;
      exitDir[i] = mag > FP64.Zero ? dir / mag : FPVector2.Zero;
    }

    return new TriangleFlowField(goalTriIndex, next, exitDir, cost);
  }

  private static FPVector2 GetPortalMidpoint(FPNavMesh navMesh, int fromTri, int toTri) {
    ref var tri = ref navMesh.Triangles[fromTri];

    for (var edge = 0; edge < 3; edge++)
      if (tri.GetNeighbor(edge) == toTri) {
        tri.GetEdgeVertices(edge, out var va, out var vb);
        var a = navMesh.Vertices[va];
        var b = navMesh.Vertices[vb];
        return new FPVector2(
          (a.x + b.x) * FP64.Half,
          (a.z + b.z) * FP64.Half);
      }

    return navMesh.Triangles[fromTri].centerXZ;
  }
}
