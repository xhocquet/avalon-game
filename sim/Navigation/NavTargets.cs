using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Deterministic.Navigation;

namespace Meesles.Avalon.Sim.Navigation;

// Resolves world points into walkable destinations.
public static class NavTargets {
  // Matches FPNavAgentSystem.
  private static readonly FP64 MultiFloorYThreshold = FP64.FromDouble(2.0);

  // MoveAlongSurface returns its start point when it cannot reach a wall.
  private static readonly FP64 NoMoveSqr = FP64.FromDouble(0.01);

  // Corners may need more than one clearance push.
  private const int MaxClearanceIterations = 4;
  private const int MaxCellRadius = 4;
  private static readonly FP64 MinPushDistance = FP64.FromDouble(0.0001);

  // Nearest walkable point without edge clearance; used for structure approach targets.
  public static FPVector3 SnapToWalkable(FPNavMeshQuery query, FPVector3 target) {
    if (query == null)
      return target;

    var snapped = query.ClosestPointOnNavMesh(target.ToXZ(), out var tri);
    return tri >= 0 ? new FPVector3(snapped.x, target.y, snapped.y) : target;
  }

  // Mover-independent, idempotent move target resolution.
  public static FPVector3 ResolveMoveTarget(FPNavMesh navMesh, FPNavMeshQuery query, FPVector3 target,
    FP64 edgeClearance) {
    if (navMesh == null || query == null)
      return target;

    var targetXz = target.ToXZ();
    if (query.FindTriangle(targetXz) >= 0)
      return WithClearance(navMesh, query, target, targetXz, edgeClearance);

    // Clamp off-map clicks before looking up nearby grid cells.
    var bounded = navMesh.BoundsXZ.ClosestPoint(targetXz);
    var closest = query.ClosestPointOnNavMesh(bounded, out var tri);
    return tri >= 0
      ? WithClearance(navMesh, query, target, closest, edgeClearance)
      : target;
  }

  // Origin-aware resolution keeps blocked clicks on the mover's side of a wall.
  public static FPVector3 ResolveMoveTarget(FPNavMesh navMesh, FPNavMeshQuery query, FPVector3 target,
    FPVector3 origin, FP64 edgeClearance) {
    if (navMesh == null || query == null)
      return target;

    var targetXz = target.ToXZ();
    if (query.FindTriangle(targetXz) >= 0)
      return WithClearance(navMesh, query, target, targetXz, edgeClearance);

    var originXz = query.ClosestPointOnNavMesh(origin.ToXZ(), out var originTri);
    if (originTri < 0)
      return ResolveMoveTarget(navMesh, query, target, edgeClearance);

    var bounded = navMesh.BoundsXZ.ClosestPoint(targetXz);
    var startPos = new FPVector3(originXz.x, FP64.Zero, originXz.y);
    var endPos = new FPVector3(bounded.x, FP64.Zero, bounded.y);
    var (resultPos, resultTri) = query.MoveAlongSurface(startPos, endPos, originTri,
      FPNavAgentSystem.DEFAULT_AREA_MASK, MultiFloorYThreshold);

    var moved = FPVector2.SqrDistance(startPos.ToXZ(), resultPos.ToXZ()) > NoMoveSqr;
    return resultTri >= 0 && moved
      ? WithClearance(navMesh, query, target, resultPos.ToXZ(), edgeClearance)
      : ResolveMoveTarget(navMesh, query, target, edgeClearance);
  }

  // Keeps the caller's Y; the agent owns height snapping.
  private static FPVector3 WithClearance(FPNavMesh navMesh, FPNavMeshQuery query, FPVector3 target,
    FPVector2 pointXz, FP64 edgeClearance) {
    var cleared = PushOffUnwalkableEdges(navMesh, query, pointXz, edgeClearance);
    return new FPVector3(cleared.x, target.y, cleared.y);
  }

  // Move targets on walls can keep an agent from settling.
  private static FPVector2 PushOffUnwalkableEdges(FPNavMesh navMesh, FPNavMeshQuery query,
    FPVector2 point, FP64 clearance) {
    if (clearance <= FP64.Zero)
      return point;

    var cellRadius = CellRadius(navMesh, clearance);
    var result = point;

    for (var i = 0; i < MaxClearanceIterations; i++) {
      if (!TryFindNearestWall(navMesh, result, clearance, cellRadius, out var wallPoint, out var wallTri))
        return result;

      var away = result - wallPoint;
      var dist = away.magnitude;
      var direction = dist > MinPushDistance
        ? away / dist
        : InwardDirection(navMesh, wallTri, wallPoint);

      var pushed = wallPoint + direction * clearance;

      // Keep the last walkable result in gaps narrower than twice the clearance.
      if (query.FindTriangle(pushed) < 0)
        return result;

      result = pushed;
    }

    return result;
  }

  // Returns the nearest boundary or blocked-neighbor edge within clearance.
  private static bool TryFindNearestWall(FPNavMesh navMesh, FPVector2 point, FP64 clearance,
    int cellRadius, out FPVector2 wallPoint, out int wallTri) {
    wallPoint = point;
    wallTri = -1;
    var bestSqr = clearance * clearance;

    navMesh.GetCellCoords(point, out var centerCol, out var centerRow);

    for (var dr = -cellRadius; dr <= cellRadius; dr++)
      for (var dc = -cellRadius; dc <= cellRadius; dc++) {
        var col = centerCol + dc;
        var row = centerRow + dr;
        if (!navMesh.IsCellValid(col, row))
          continue;

        navMesh.GetCellTriangles(col, row, out var start, out var count);
        for (var i = 0; i < count; i++) {
          var triIdx = navMesh.GridTriangles[start + i];
          ref readonly var tri = ref navMesh.Triangles[triIdx];
          if (tri.isBlocked)
            continue;

          for (var e = 0; e < 3; e++) {
            var neighbor = tri.GetNeighbor(e);
            if (neighbor >= 0 && !navMesh.Triangles[neighbor].isBlocked)
              continue;

            tri.GetEdgeVertices(e, out var va, out var vb);
            var closest = FPNavMeshQuery.ClosestPointOnSegment2D(point,
              navMesh.Vertices[va].ToXZ(), navMesh.Vertices[vb].ToXZ());

            var sqr = FPVector2.SqrDistance(point, closest);
            if (sqr >= bestSqr)
              continue;

            bestSqr = sqr;
            wallPoint = closest;
            wallTri = triIdx;
          }
        }
      }

    return wallTri >= 0;
  }

  // Points exactly on an edge move toward the triangle centroid.
  private static FPVector2 InwardDirection(FPNavMesh navMesh, int triIdx, FPVector2 wallPoint) {
    var toCenter = navMesh.Triangles[triIdx].centerXZ - wallPoint;
    return toCenter.sqrMagnitude > FP64.Zero ? toCenter.normalized : FPVector2.Zero;
  }

  // Wider clearances search more grid cells.
  private static int CellRadius(FPNavMesh navMesh, FP64 clearance) {
    var radius = 1;
    while (radius < MaxCellRadius && FP64.FromInt(radius) * navMesh.GridCellSize < clearance)
      radius++;

    return radius;
  }
}
