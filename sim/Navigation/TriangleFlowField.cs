using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Deterministic.Navigation;

namespace Meesles.Avalon.Sim.Navigation {
  public sealed class TriangleFlowField {
    public const int AT_GOAL = -1;
    public const int UNREACHABLE = -2;

    public readonly int GoalTriangleIndex;
    public readonly int[] NextTriangle;
    public readonly FPVector2[] ExitDirection;
    public readonly FP64[] Cost;

    private TriangleFlowField(int goalTriangleIndex, int[] nextTriangle, FPVector2[] exitDirection, FP64[] cost) {
      GoalTriangleIndex = goalTriangleIndex;
      NextTriangle = nextTriangle;
      ExitDirection = exitDirection;
      Cost = cost;
    }

    public static TriangleFlowField Compute(FPNavMesh navMesh, int goalTriIndex) {
      int triCount = navMesh.Triangles.Length;
      var next = new int[triCount];
      var exitDir = new FPVector2[triCount];
      var cost = new FP64[triCount];

      for (int i = 0; i < triCount; i++) {
        next[i] = UNREACHABLE;
        cost[i] = FP64.MaxValue;
      }

      next[goalTriIndex] = AT_GOAL;
      cost[goalTriIndex] = FP64.Zero;

      // Dijkstra BFS from goal outward using a simple priority queue.
      // With <1000 triangles, an array-scan "queue" is fast enough and avoids allocations.
      var open = new bool[triCount];
      open[goalTriIndex] = true;
      int openCount = 1;

      while (openCount > 0) {
        // Find lowest-cost open node
        int current = -1;
        FP64 bestCost = FP64.MaxValue;
        for (int i = 0; i < triCount; i++) {
          if (open[i] && cost[i] < bestCost) {
            bestCost = cost[i];
            current = i;
          }
        }

        if (current < 0)
          break;

        open[current] = false;
        openCount--;

        ref var currentTri = ref navMesh.Triangles[current];

        for (int edge = 0; edge < 3; edge++) {
          int neighborIdx = currentTri.GetNeighbor(edge);
          if (neighborIdx < 0)
            continue;

          ref var neighborTri = ref navMesh.Triangles[neighborIdx];
          if (neighborTri.isBlocked)
            continue;

          FPVector2 delta = neighborTri.centerXZ - currentTri.centerXZ;
          FP64 edgeCost = delta.magnitude * neighborTri.costMultiplier;
          FP64 newCost = cost[current] + edgeCost;

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
      for (int i = 0; i < triCount; i++) {
        if (next[i] < 0) {
          exitDir[i] = FPVector2.Zero;
          continue;
        }

        int nextTri = next[i];
        FPVector2 portalMid = GetPortalMidpoint(navMesh, i, nextTri);
        FPVector2 dir = portalMid - navMesh.Triangles[i].centerXZ;
        FP64 mag = dir.magnitude;
        exitDir[i] = mag > FP64.Zero ? dir / mag : FPVector2.Zero;
      }

      return new TriangleFlowField(goalTriIndex, next, exitDir, cost);
    }

    private static FPVector2 GetPortalMidpoint(FPNavMesh navMesh, int fromTri, int toTri) {
      ref var tri = ref navMesh.Triangles[fromTri];

      for (int edge = 0; edge < 3; edge++) {
        if (tri.GetNeighbor(edge) == toTri) {
          tri.GetEdgeVertices(edge, out int va, out int vb);
          FPVector3 a = navMesh.Vertices[va];
          FPVector3 b = navMesh.Vertices[vb];
          return new FPVector2(
            (a.x + b.x) * FP64.Half,
            (a.z + b.z) * FP64.Half);
        }
      }

      return navMesh.Triangles[fromTri].centerXZ;
    }
  }
}
