using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Deterministic.Navigation;

namespace Meesles.Avalon.Sim.Navigation;

// Turns a world point — a right-click, a structure's centre — into somewhere a nav agent can
// actually stand. The client resolves a move order through the same entry point the sim does, so
// the click marker lands on the destination the unit will be given rather than on the raw click.
public static class NavTargets {
  // Matches FPNavAgentSystem's own threshold, so a step the authoritative navigation treats as a
  // wall is treated as one here too.
  private static readonly FP64 MultiFloorYThreshold = FP64.FromDouble(2.0);

  // Planar distance (squared) under which a MoveAlongSurface result counts as "didn't move" — its
  // bounded BFS returns the start point when it never reaches a wall, which is not a destination.
  private static readonly FP64 NoMoveSqr = FP64.FromDouble(0.01);

  // Clearing one wall can leave the point inside another's band, so corners need more than one push.
  private const int MaxClearanceIterations = 4;
  private const int MaxCellRadius = 4;
  private static readonly FP64 MinPushDistance = FP64.FromDouble(0.0001);

  // Nearest walkable point, no edge clearance. Structure approach targets use this: the attacker's
  // range check clears the move target on the near rim, so breathing room buys nothing there.
  //
  // Snapped to the geometrically nearest walkable point rather than the rim facing the mover: the
  // result has to stay constant per target, or a mover-relative point would shift every tick and
  // repath-throttle the unit into standing still.
  public static FPVector3 SnapToWalkable(FPNavMeshQuery query, FPVector3 target) {
    if (query == null)
      return target;

    var snapped = query.ClosestPointOnNavMesh(target.ToXZ(), out var tri);
    return tri >= 0 ? new FPVector3(snapped.x, target.y, snapped.y) : target;
  }

  // Move-order resolution, mover-independent and idempotent: clamp into the navmesh bounds, snap to
  // the nearest walkable point, then back off `edgeClearance` from the nearest unwalkable edge.
  // Re-running it on its own result returns that result, which is what lets the client resolve the
  // click and the sim re-resolve the command without the two disagreeing.
  public static FPVector3 ResolveMoveTarget(FPNavMesh navMesh, FPNavMeshQuery query, FPVector3 target,
    FP64 edgeClearance) {
    if (navMesh == null || query == null)
      return target;

    var targetXZ = target.ToXZ();
    if (query.FindTriangle(targetXZ) >= 0)
      return WithClearance(navMesh, query, target, targetXZ, edgeClearance);

    // A click well past the map edge has no nearby grid cells to search, so the closest-point snap
    // would find nothing; clamping to the bounds lands it on the perimeter where the boundary
    // triangles are. Clicks inside an obstacle island are already in the box and pass through.
    var bounded = navMesh.BoundsXZ.ClosestPoint(targetXZ);
    var closest = query.ClosestPointOnNavMesh(bounded, out var tri);
    return tri >= 0
      ? WithClearance(navMesh, query, target, closest, edgeClearance)
      : target;
  }

  // Same, anchored on the unit doing the moving: a click behind a wall lands on the mover's side of
  // it rather than on whichever edge happens to be geometrically nearest. Falls back to the
  // mover-independent resolution, so the result is still a fixed point of it.
  public static FPVector3 ResolveMoveTarget(FPNavMesh navMesh, FPNavMeshQuery query, FPVector3 target,
    FPVector3 origin, FP64 edgeClearance) {
    if (navMesh == null || query == null)
      return target;

    var targetXZ = target.ToXZ();
    if (query.FindTriangle(targetXZ) >= 0)
      return WithClearance(navMesh, query, target, targetXZ, edgeClearance);

    var originXZ = query.ClosestPointOnNavMesh(origin.ToXZ(), out var originTri);
    if (originTri < 0)
      return ResolveMoveTarget(navMesh, query, target, edgeClearance);

    var bounded = navMesh.BoundsXZ.ClosestPoint(targetXZ);
    var startPos = new FPVector3(originXZ.x, FP64.Zero, originXZ.y);
    var endPos = new FPVector3(bounded.x, FP64.Zero, bounded.y);
    var (resultPos, resultTri) = query.MoveAlongSurface(startPos, endPos, originTri, MultiFloorYThreshold);

    var moved = FPVector2.SqrDistance(startPos.ToXZ(), resultPos.ToXZ()) > NoMoveSqr;
    return resultTri >= 0 && moved
      ? WithClearance(navMesh, query, target, resultPos.ToXZ(), edgeClearance)
      : ResolveMoveTarget(navMesh, query, target, edgeClearance);
  }

  // Keeps the caller's y: destinations are planar, and the agent's own snap owns the height.
  private static FPVector3 WithClearance(FPNavMesh navMesh, FPNavMeshQuery query, FPVector3 target,
    FPVector2 pointXZ, FP64 edgeClearance) {
    var cleared = PushOffUnwalkableEdges(navMesh, query, pointXZ, edgeClearance);
    return new FPVector3(cleared.x, target.y, cleared.y);
  }

  // Both snap paths land exactly ON the boundary, and a destination sitting on a wall leaves the
  // agent grinding along it — avoidance and the agent's own radius keep pushing it off the point it
  // is trying to reach, and it never arrives. Back off until the nearest unwalkable edge is
  // `clearance` away.
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

      // A gap narrower than twice the clearance has no point that satisfies both its walls; keep the
      // one found so far rather than stepping off the mesh entirely.
      if (query.FindTriangle(pushed) < 0)
        return result;

      result = pushed;
    }

    return result;
  }

  // Closest point on the nearest wall edge within `clearance`, plus the walkable triangle it bounds.
  // A wall is a boundary edge or one facing a blocked triangle — the same test MoveAlongSurface uses.
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
          ref var tri = ref navMesh.Triangles[triIdx];
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

  // "Straight off the wall" is undefined for a point sitting exactly on it; aim at the bounded
  // triangle's centroid instead, which is always on the walkable side.
  private static FPVector2 InwardDirection(FPNavMesh navMesh, int triIdx, FPVector2 wallPoint) {
    var toCenter = navMesh.Triangles[triIdx].centerXZ - wallPoint;
    return toCenter.sqrMagnitude > FP64.Zero ? toCenter.normalized : FPVector2.Zero;
  }

  // The wall search reads whole grid cells, so a clearance wider than one cell needs a wider scan.
  private static int CellRadius(FPNavMesh navMesh, FP64 clearance) {
    var radius = 1;
    while (radius < MaxCellRadius && FP64.FromInt(radius) * navMesh.GridCellSize < clearance)
      radius++;

    return radius;
  }
}
