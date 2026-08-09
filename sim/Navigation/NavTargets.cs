using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Deterministic.Navigation;

namespace Meesles.Avalon.Sim.Navigation;

// Helps handle navigating to structures which have gaps in the navmesh
public static class NavTargets {
  // Snapped to the geometrically nearest walkable point rather than the rim facing the mover: the
  // result has to stay constant per target, or a mover-relative point would shift every tick and
  // repath-throttle the unit into standing still. Approach side doesn't matter anyway — the range
  // check clears the move target as soon as the unit is close enough, which happens on the near rim.
  public static FPVector3 SnapToWalkable(FPNavMeshQuery query, FPVector3 target) {
    if (query == null)
      return target;

    var snapped = query.ClosestPointOnNavMesh(target.ToXZ(), out var tri);
    return tri >= 0 ? new FPVector3(snapped.x, target.y, snapped.y) : target;
  }
}
