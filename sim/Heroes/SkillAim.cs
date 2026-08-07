using Meesles.Avalon.Sim.Assets;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Heroes;

// The planar geometry between a caster and the aim point that came off CastSkillCommand. Sits at the
// root of Heroes/ so both SkillActions and any hero's skill set read the aim the same way.
public static class SkillAim {
  // Pulls the aim point onto the skill's cast band along the line the client aimed. An unbounded end
  // (0 on the row) leaves that side alone, so a skill authoring neither keeps the raw point and only
  // the world envelope applies. Always returns a flattened point.
  public static FPVector3 ClampToCastRange(ref Frame frame, EntityRef caster, SkillAsset skill,
    FPVector3 casterPosition, FPVector3 target) {
    target.y = FP64.Zero;
    if (skill == null || !skill.HasCastRange)
      return target;

    var toTarget = target - casterPosition;
    toTarget.y = FP64.Zero;
    var distanceSqr = toTarget.sqrMagnitude;

    var max = skill.MaxCastRange;
    if (max > FP64.Zero && distanceSqr > max * max)
      return PointAt(ref frame, caster, casterPosition, toTarget, max);

    var min = skill.MinCastRange;
    if (min > FP64.Zero && distanceSqr < min * min)
      return PointAt(ref frame, caster, casterPosition, toTarget, min);

    return target;
  }

  // Planar direction from the caster to the aim point. Aiming at your own feet fires straight ahead
  // rather than firing nowhere - Rotation is the Atan2(x, z) yaw every mover writes.
  public static FPVector3 Direction(ref Frame frame, EntityRef caster, FPVector3 from, FPVector3 to) {
    var toTarget = to - from;
    toTarget.y = FP64.Zero;
    return Direction(ref frame, caster, toTarget);
  }

  private static FPVector3 Direction(ref Frame frame, EntityRef caster, FPVector3 toTarget) {
    if (toTarget.sqrMagnitude > FP64.Zero)
      return toTarget.normalized;

    var yaw = frame.Has<TransformComponent>(caster)
      ? frame.GetReadOnly<TransformComponent>(caster).Rotation
      : FP64.Zero;
    return new FPVector3(FP64.Sin(yaw), FP64.Zero, FP64.Cos(yaw));
  }

  private static FPVector3 PointAt(ref Frame frame, EntityRef caster, FPVector3 casterPosition,
    FPVector3 toTarget, FP64 distance) {
    var direction = Direction(ref frame, caster, toTarget);
    return new FPVector3(casterPosition.x + direction.x * distance, FP64.Zero,
      casterPosition.z + direction.z * distance);
  }
}
