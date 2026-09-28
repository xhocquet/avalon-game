using Meesles.Avalon.Sim.Assets;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

// Skill targeting and aim direction.
public static class SkillAim {
  // Clamps aim to the skill's cast range and flattens it to XZ.
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

  // Uses caster facing when aimed at the caster's feet.
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
