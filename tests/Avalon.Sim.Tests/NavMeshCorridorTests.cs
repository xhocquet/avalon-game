using FluentAssertions;
using Xunit;
using xpTURN.Klotho.Deterministic.Geometry;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Deterministic.Navigation;

namespace Meesles.Avalon.Sim.Tests;

// Regression coverage for the FPNavMeshPathfinder corridor buffer (MAX_CORRIDOR = 128).
// Builds a synthetic "ladder" navmesh strip long enough that the shortest route between
// its two ends must cross more triangles than the corridor buffer can hold, independent
// of how big the real baked game map happens to be.
public class NavMeshCorridorTests {
  // 200 quads (400 triangles) — comfortably more than FPNavMeshPathfinder.MAX_CORRIDOR (128).
  private const int QuadCount = 200;

  [Fact]
  public void FindPath_RouteLongerThanCorridorBuffer_StillConnectsStartToEnd() {
    var navMesh = BuildStripNavMesh(QuadCount);
    var query = new FPNavMeshQuery(navMesh, logger: null);
    var pathfinder = new FPNavMeshPathfinder(navMesh, query, logger: null);

    FPVector3 start = new FPVector3(FP64.FromDouble(0.5), FP64.Zero, FP64.FromDouble(0.5));
    FPVector3 end = new FPVector3(FP64.FromDouble(QuadCount - 0.5), FP64.Zero, FP64.FromDouble(0.5));

    int startTri = query.FindTriangle(start.ToXZ(), start.y);
    int endTri = query.FindTriangle(end.ToXZ(), end.y);
    startTri.Should().BeGreaterThanOrEqualTo(0, "test setup should place start inside the strip");
    endTri.Should().BeGreaterThanOrEqualTo(0, "test setup should place end inside the strip");

    bool found = pathfinder.FindPath(start, end, FPNavAgentSystem.DEFAULT_AREA_MASK,
      out int[] corridor, out int corridorLength);

    found.Should().BeTrue("a path exists along the strip even though it exceeds the corridor buffer size");
    corridor[0].Should().Be(startTri,
      "the returned corridor must begin at the agent's actual triangle so movement/off-corridor tracking " +
      "stays in sync, even when the full route is longer than the corridor buffer can hold");
    corridorLength.Should().Be(FPNavMeshPathfinder.MAX_CORRIDOR,
      "the route is longer than the buffer, so it should fill the buffer completely starting from the agent " +
      "rather than reach the destination triangle in one shot (the caller repaths for the remaining distance)");

    for (int i = 0; i < corridorLength - 1; i++) {
      bool adjacent = navMesh.Triangles[corridor[i]].GetNeighbor(0) == corridor[i + 1]
        || navMesh.Triangles[corridor[i]].GetNeighbor(1) == corridor[i + 1]
        || navMesh.Triangles[corridor[i]].GetNeighbor(2) == corridor[i + 1];
      adjacent.Should().BeTrue($"corridor[{i}]={corridor[i]} and corridor[{i + 1}]={corridor[i + 1]} must share an edge");
    }
  }

  [Fact]
  public void FindPath_RouteWithinCorridorBuffer_ReachesActualEndpoints() {
    const int shortQuadCount = 5;
    var navMesh = BuildStripNavMesh(shortQuadCount);
    var query = new FPNavMeshQuery(navMesh, logger: null);
    var pathfinder = new FPNavMeshPathfinder(navMesh, query, logger: null);

    FPVector3 start = new FPVector3(FP64.FromDouble(0.5), FP64.Zero, FP64.FromDouble(0.5));
    FPVector3 end = new FPVector3(FP64.FromDouble(shortQuadCount - 0.5), FP64.Zero, FP64.FromDouble(0.5));

    int startTri = query.FindTriangle(start.ToXZ(), start.y);
    int endTri = query.FindTriangle(end.ToXZ(), end.y);

    bool found = pathfinder.FindPath(start, end, FPNavAgentSystem.DEFAULT_AREA_MASK,
      out int[] corridor, out int corridorLength);

    found.Should().BeTrue();
    corridor[0].Should().Be(startTri);
    corridor[corridorLength - 1].Should().Be(endTri,
      "a route that fits comfortably within the corridor buffer should still resolve end-to-end");
  }

  // Builds a 1-wide, QuadCount-long strip of quads (2 triangles each) running along +X,
  // from (0,0)-(0,1) to (QuadCount,0)-(QuadCount,1). Traversing end-to-end requires
  // crossing every quad in sequence — there is no shortcut.
  private static FPNavMesh BuildStripNavMesh(int quadCount) {
    int vertexCount = (quadCount + 1) * 2;
    var vertices = new FPVector3[vertexCount];
    for (int j = 0; j <= quadCount; j++) {
      vertices[2 * j] = new FPVector3(j, 0, 0);     // bottom row
      vertices[2 * j + 1] = new FPVector3(j, 0, 1); // top row
    }

    int triangleCount = quadCount * 2;
    var triangles = new FPNavMeshTriangle[triangleCount];
    for (int j = 0; j < quadCount; j++) {
      int bottomJ = 2 * j;
      int bottomJ1 = 2 * (j + 1);
      int topJ = 2 * j + 1;
      int topJ1 = 2 * (j + 1) + 1;

      int triA = 2 * j;
      int triB = 2 * j + 1;

      triangles[triA] = MakeTriangle(bottomJ, bottomJ1, topJ,
        neighbor0: -1,
        neighbor1: triB,
        neighbor2: j > 0 ? triB - 2 : -1,
        vertices);

      triangles[triB] = MakeTriangle(bottomJ1, topJ1, topJ,
        neighbor0: j + 1 < quadCount ? triA + 2 : -1,
        neighbor1: -1,
        neighbor2: triA,
        vertices);
    }

    var boundsXz = new FPBounds2(
      new FPVector2(FP64.FromDouble(quadCount / 2.0), FP64.FromDouble(0.5)),
      new FPVector2(FP64.FromInt(quadCount), FP64.One));

    // One grid cell per quad column; each cell holds that quad's two triangles.
    var gridCells = new int[quadCount * 2];
    var gridTriangles = new int[triangleCount];
    for (int j = 0; j < quadCount; j++) {
      gridCells[j * 2] = 2 * j;
      gridCells[j * 2 + 1] = 2;
      gridTriangles[2 * j] = 2 * j;
      gridTriangles[2 * j + 1] = 2 * j + 1;
    }

    return new FPNavMesh(
      vertices,
      triangles,
      boundsXz,
      gridCells,
      gridTriangles,
      gridWidth: quadCount,
      gridHeight: 1,
      gridCellSize: FP64.One,
      gridOrigin: FPVector2.Zero);
  }

  private static FPNavMeshTriangle MakeTriangle(int v0, int v1, int v2,
      int neighbor0, int neighbor1, int neighbor2, FPVector3[] vertices) {
    FPVector2 a = vertices[v0].ToXZ();
    FPVector2 b = vertices[v1].ToXZ();
    FPVector2 c = vertices[v2].ToXZ();
    FPVector2 centerXz = (a + b + c) / FP64.FromInt(3);

    return new FPNavMeshTriangle {
      v0 = v0,
      v1 = v1,
      v2 = v2,
      neighbor0 = neighbor0,
      neighbor1 = neighbor1,
      neighbor2 = neighbor2,
      centerXZ = centerXz,
      area = FP64.One,
      areaMask = FPNavAgentSystem.DEFAULT_AREA_MASK,
      costMultiplier = FP64.One,
      isBlocked = false,
      minY = FP64.Zero,
      maxY = FP64.Zero,
      centerY = FP64.Zero,
    };
  }
}
