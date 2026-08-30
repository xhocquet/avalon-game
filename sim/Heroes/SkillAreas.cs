using System.Collections.Generic;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Heroes;

// Shared disc search, the round sibling of SkillCones: every hero and minion standing inside a circle
// on the ground, in filter order. Kept separate from what is done to the hits, because an area effect
// resolving on a later tick than the cast that armed it has no SkillCastContext left to pass around.
public static class SkillAreas {
  // Collects every hostile hero and minion whose body overlaps the disc. Reach is widened by the
  // target's own gameplay radius, the same way a cone's is, so a wide body clipping the rim counts.
  public static void Collect(ref Frame frame, EntityRef caster, FPVector3 center, FP64 radius,
    List<EntityRef> hits) {
    CollectSide(ref frame, caster, center, radius, hits, allied: false);
  }

  // The allied side: the caster and every friendly hero and minion in the disc.
  public static void CollectAllies(ref Frame frame, EntityRef caster, FPVector3 center, FP64 radius,
    List<EntityRef> hits) {
    CollectSide(ref frame, caster, center, radius, hits, allied: true);
  }

  private static void CollectSide(ref Frame frame, EntityRef caster, FPVector3 center, FP64 radius,
    List<EntityRef> hits, bool allied) {
    hits.Clear();
    if (radius <= FP64.Zero)
      return;

    var origin = center.ToXZ();
    var filter = frame.Filter<UnitIdComponent, TeamComponent, Health, TransformComponent>();
    while (filter.Next(out var candidate)) {
      if (!CombatTargeting.IsSkillHittable(ref frame, candidate))
        continue;

      var onSide = allied
        ? CombatTargeting.IsAlliedAndAlive(ref frame, caster, candidate)
        : CombatTargeting.IsHostileAndAlive(ref frame, caster, candidate);
      if (!onSide)
        continue;

      var offset = frame.GetReadOnly<TransformComponent>(candidate).Position.ToXZ() - origin;
      var reach = radius + CombatRange.GameplayRadiusOf(ref frame, candidate);
      if (offset.sqrMagnitude <= reach * reach)
        hits.Add(candidate);
    }
  }
}
