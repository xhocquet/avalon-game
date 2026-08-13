using System.Collections.Generic;
using Meesles.Avalon.Sim.Assets;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Deterministic.Navigation;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Navigation;

// One member of a group move. Position feeds the group centroid and nothing else; the centroid is
// what gives the formation its facing.
public readonly struct FormationUnit(EntityRef entity, int unitId, bool isHero, FPVector3 position) {
  public readonly EntityRef Entity = entity;
  public readonly int UnitId = unitId;
  public readonly bool IsHero = isHero;
  public readonly FPVector3 Position = position;
}

// Lays out a selected group around a move order: heroes on the click itself, minions blobbed up
// behind them.
//
// Minions all share one destination instead of getting precomputed slots. Slots looked bad — a
// minion eases up to its exact point, gets ORCA-blocked, then lurches the last bit once a
// neighbour clears — and the assignment goes stale over a long march anyway. With a shared
// destination ORCA packs them wherever they fit and NavigationAgentSystem's settle logic freezes
// them there.
public static class GroupFormation {
  private static readonly FPVector2 DefaultForward = new(FP64.Zero, FP64.One);

  // Sorts `units` in place (heroes first, then unit id, so peers agree on the order) and fills
  // `destinations` with one target per unit, index-aligned to the sorted list. A null `navMesh` or
  // `query` skips navmesh snapping.
  public static void Solve(List<FormationUnit> units, FPVector3 target, MovementRulesAsset rules,
    FPNavMesh navMesh, FPNavMeshQuery query, List<FPVector3> destinations) {
    units.Sort(CompareUnits);
    destinations.Clear();

    var forward = GetForward(units, target);
    var right = new FPVector2(forward.y, -forward.x);
    var heroCount = CountHeroes(units);
    var minionTarget =
      GetMinionTarget(units.Count - heroCount, heroCount, target, forward, rules, navMesh, query);

    var heroIndex = 0;
    for (var i = 0; i < units.Count; i++) {
      if (!units[i].IsHero) {
        destinations.Add(minionTarget);
        continue;
      }

      var lateral = GetCenteredOffset(heroIndex++, heroCount, rules.HeroLateralSpacing);
      var heroXz = target.ToXZ() + right * lateral;
      destinations.Add(SnapToNavMesh(new FPVector3(heroXz.x, target.y, heroXz.y), rules, navMesh, query));
    }
  }

  private static int CompareUnits(FormationUnit a, FormationUnit b) {
    if (a.IsHero != b.IsHero)
      return a.IsHero ? -1 : 1;

    return a.UnitId.CompareTo(b.UnitId);
  }

  // Direction of travel: centroid toward the click. An empty group has no centroid, and clicking
  // the centroid itself yields no direction; both fall back to +Z.
  private static FPVector2 GetForward(List<FormationUnit> units, FPVector3 target) {
    if (units.Count == 0)
      return DefaultForward;

    var centroid = FPVector3.Zero;
    for (var i = 0; i < units.Count; i++)
      centroid += units[i].Position;
    centroid /= FP64.FromInt(units.Count);

    var forward = (target - centroid).ToXZ();
    return forward.sqrMagnitude > FP64.Zero ? forward.normalized : DefaultForward;
  }

  // Back the blob off far enough that its front edge clears the hero. No hero, no offset.
  private static FPVector3 GetMinionTarget(int minionCount, int heroCount, FPVector3 target,
    FPVector2 forward, MovementRulesAsset rules, FPNavMesh navMesh, FPNavMeshQuery query) {
    if (heroCount == 0)
      return SnapToNavMesh(target, rules, navMesh, query);

    var blobRadius = FP64.Sqrt(FP64.FromInt(minionCount > 0 ? minionCount : 1)) * rules.MinionPackRadiusFactor;
    var minionXZ = target.ToXZ() - forward * (blobRadius + rules.HeroClearance);
    return SnapToNavMesh(new FPVector3(minionXZ.x, target.y, minionXZ.y), rules, navMesh, query);
  }

  private static int CountHeroes(List<FormationUnit> units) {
    var count = 0;
    for (var i = 0; i < units.Count; i++)
      if (units[i].IsHero)
        count++;

    return count;
  }

  private static FP64 GetCenteredOffset(int index, int count, FP64 spacing) {
    return FP64.FromInt(index * 2 - (count - 1)) * spacing * FP64.Half;
  }

  // A null query is the direct-move test path, which never pathfinds — hand the slot back as-is.
  // Formation slots are offset off the click, so a slot can land inside an obstacle the click
  // cleared; they get the same edge clearance the click itself was resolved with.
  private static FPVector3 SnapToNavMesh(FPVector3 slot, MovementRulesAsset rules, FPNavMesh navMesh,
    FPNavMeshQuery query) {
    return NavTargets.ResolveMoveTarget(navMesh, query, slot, rules.MoveTargetEdgeClearance);
  }
}
