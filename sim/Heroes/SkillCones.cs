using System.Collections.Generic;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Heroes;

// Shared cone lifecycle: one instant wedge in front of the caster, resolved at cast time rather than
// travelling. Kept at the root of Heroes/ beside SkillProjectiles so any hero's skill set can swing
// one without owning the hit search.
public static class SkillCones {
  // Damages every hostile hero and minion inside the wedge. `angleDegrees` is the full opening angle
  // and `range` the reach from the caster's centre, widened by the target's own body - bearing is
  // measured to the centre, so a wide body is easier to catch at the far edge than at the sides.
  public static void ApplyDamage(ref Frame frame, in SkillCastContext ctx, FPVector3 direction,
    FP64 range, FP64 angleDegrees, FP64 damage, DamageType damageType = DamageType.Magical) {
    if (range <= FP64.Zero || angleDegrees <= FP64.Zero || damage <= FP64.Zero)
      return;

    var facing = new FPVector2(direction.x, direction.z);
    if (facing.sqrMagnitude <= FP64.Zero)
      return;
    facing = facing.normalized;

    var origin = ctx.CasterPosition.ToXZ();
    var cosHalfAngle = FP64.Cos(angleDegrees * FP64.Deg2Rad / FP64.FromInt(2));

    // Collected first, damaged after: ApplyDamage allocates the hit-id singleton on its first call of
    // the match, and that creates an entity while the filter is still walking storage.
    var hits = new List<EntityRef>();
    var filter = frame.Filter<UnitIdentity, Team, Health, TransformComponent>();
    while (filter.Next(out var candidate)) {
      if (!CombatTargeting.IsSkillHittable(ref frame, candidate))
        continue;
      if (!CombatTargeting.IsHostileAndAlive(ref frame, ctx.Caster, candidate))
        continue;
      if (IsInside(ref frame, candidate, origin, facing, range, cosHalfAngle))
        hits.Add(candidate);
    }

    foreach (var target in hits)
      DamageApplication.ApplyDamage(ref frame, ctx.Caster, target, damage, damageType);
  }

  private static bool IsInside(ref Frame frame, EntityRef target, FPVector2 origin, FPVector2 facing,
    FP64 range, FP64 cosHalfAngle) {
    var offset = frame.GetReadOnly<TransformComponent>(target).Position.ToXZ() - origin;
    var body = CombatRange.GameplayRadiusOf(ref frame, target);

    var reach = range + body;
    var distanceSq = offset.sqrMagnitude;
    if (distanceSq > reach * reach)
      return false;

    // Overlapping the caster: no meaningful bearing to test, and a wedge that misses what is standing
    // on top of it reads as a miss.
    if (distanceSq <= body * body)
      return true;

    // dot >= |offset| * cos(half), squared so the per-target test keeps the square root out. An angle
    // past 180 puts cos below zero and flips which side of the comparison the wedge is on.
    var dot = FPVector2.Dot(offset, facing);
    var threshold = cosHalfAngle * cosHalfAngle * distanceSq;
    return cosHalfAngle >= FP64.Zero
      ? dot > FP64.Zero && dot * dot >= threshold
      : dot >= FP64.Zero || dot * dot <= threshold;
  }
}
