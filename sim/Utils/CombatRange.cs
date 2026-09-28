using System.Collections.Generic;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

// Range helpers. Attack range calculated edge-to-edge
public static class CombatRange {
  // Centre distance the attacker has to close to, squared for the caller's squared distance.
  public static FP64 ReachSq(ref Frame frame, EntityRef attacker, EntityRef target) {
    var reach = AttackRangeOf(ref frame, attacker) +
                GameplayRadiusOf(ref frame, attacker) +
                GameplayRadiusOf(ref frame, target);
    return reach * reach;
  }

  // Range measured on the XZ plane, the way every order and swing does. False when either side has
  // no transform to measure from, so a caller never treats a missing position as point-blank.
  public static bool IsWithinReach(
    ref Frame frame, EntityRef attacker, EntityRef target, out FP64 distSq, out FP64 rangeSq) {
    distSq = FP64.Zero;
    rangeSq = FP64.Zero;
    if (!frame.Has<TransformComponent>(attacker) || !frame.Has<TransformComponent>(target))
      return false;

    ref readonly var attackerTransform = ref frame.GetReadOnly<TransformComponent>(attacker);
    ref readonly var targetTransform = ref frame.GetReadOnly<TransformComponent>(target);

    distSq = Planar.DistanceSq(attackerTransform.Position, targetTransform.Position);
    rangeSq = ReachSq(ref frame, attacker, target);
    return distSq <= rangeSq;
  }

  public static FP64 GameplayRadiusOf(ref Frame frame, EntityRef entity) {
    return frame.Has<Stats>(entity)
      ? frame.GetReadOnly<Stats>(entity).GameplayRadius
      : FP64.Zero;
  }

  public static bool IsWithinCone(ref Frame frame, EntityRef target, FPVector2 origin, FPVector2 facing,
    FP64 range, FP64 cosHalfAngle) {
    var offset = frame.GetReadOnly<TransformComponent>(target).Position.ToXZ() - origin;
    var body = GameplayRadiusOf(ref frame, target);
    var distanceSq = offset.sqrMagnitude;
    var reach = range + body;
    if (distanceSq > reach * reach)
      return false;

    if (distanceSq <= body * body)
      return true;

    var dot = FPVector2.Dot(offset, facing);
    var threshold = cosHalfAngle * cosHalfAngle * distanceSq;
    return cosHalfAngle >= FP64.Zero
      ? dot > FP64.Zero && dot * dot >= threshold
      : dot >= FP64.Zero || dot * dot <= threshold;
  }

  public static void CollectHostilesInRadius(ref Frame frame, EntityRef caster, FPVector3 center,
    FP64 radius, List<EntityRef> hits) {
    CollectInRadius(ref frame, caster, center, radius, hits, allied: false);
  }

  public static void CollectAlliesInRadius(ref Frame frame, EntityRef caster, FPVector3 center,
    FP64 radius, List<EntityRef> hits) {
    CollectInRadius(ref frame, caster, center, radius, hits, allied: true);
  }

  private static void CollectInRadius(ref Frame frame, EntityRef caster, FPVector3 center,
    FP64 radius, List<EntityRef> hits, bool allied) {
    hits.Clear();
    if (radius <= FP64.Zero)
      return;

    var origin = center.ToXZ();
    var filter = frame.Filter<UnitIdentity, Team, Health, TransformComponent>();
    while (filter.Next(out var candidate)) {
      if (!CombatTargeting.IsSkillHittable(ref frame, candidate))
        continue;

      var onSide = allied
        ? CombatTargeting.IsAlliedAndAlive(ref frame, caster, candidate)
        : CombatTargeting.IsHostileAndAlive(ref frame, caster, candidate);
      if (!onSide)
        continue;

      var offset = frame.GetReadOnly<TransformComponent>(candidate).Position.ToXZ() - origin;
      var reach = radius + GameplayRadiusOf(ref frame, candidate);
      if (offset.sqrMagnitude <= reach * reach)
        hits.Add(candidate);
    }
  }

  private static FP64 AttackRangeOf(ref Frame frame, EntityRef entity) {
    return frame.Has<Stats>(entity)
      ? frame.GetReadOnly<Stats>(entity).AttackRange
      : FP64.Zero;
  }
}
