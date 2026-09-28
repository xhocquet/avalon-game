using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

public static class Planar {
  public static FP64 DistanceSq(FPVector2 from, FPVector2 to) {
    return (to - from).sqrMagnitude;
  }

  public static FP64 DistanceSq(FPVector3 from, FPVector3 to) {
    var offset = to - from;
    offset.y = FP64.Zero;
    return offset.sqrMagnitude;
  }

  public static FP64 ClosestPointTravel(FPVector2 start, FPVector2 end, FPVector2 point) {
    var segment = end - start;
    var lengthSq = segment.sqrMagnitude;
    return lengthSq > FP64.Zero
      ? FP64.Clamp01(FPVector2.Dot(point - start, segment) / lengthSq)
      : FP64.Zero;
  }

  public static bool MoveTowards(ref TransformComponent transform, FPVector3 destination, FP64 step,
    FP64 stopDistance = default) {
    var offset = destination - transform.Position;
    offset.y = FP64.Zero;
    var distance = offset.magnitude;
    if (distance <= stopDistance)
      return true;

    if (step >= distance) {
      transform.Position = destination;
      return true;
    }

    var movement = offset.normalized * step;
    transform.Position += movement;
    transform.Rotation = FP64.Atan2(movement.x, movement.z);
    return false;
  }
}
