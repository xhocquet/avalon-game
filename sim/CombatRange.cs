using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

// Attack range is authored edge-to-edge: the reach a unit has past its own body, to the target's
// body. Measuring centre-to-centre instead makes a melee unit unable to touch anything wider than
// its reach - a turret sits in a hole in the navmesh 1.6m across, so a 1.25m reach never lands.
public static class CombatRange {
  // Centre distance the attacker has to close to, squared for the caller's squared distance.
  public static FP64 ReachSq(ref Frame frame, EntityRef attacker, EntityRef target) {
    var reach = AttackRangeOf(ref frame, attacker) + GameplayRadiusOf(ref frame, attacker) +
                GameplayRadiusOf(ref frame, target);
    return reach * reach;
  }

  // Range measured on the XZ plane, the way every order and swing does. False when either side has
  // no transform to measure from, so a caller never treats a missing position as point-blank.
  public static bool IsWithinReach(ref Frame frame, EntityRef attacker, EntityRef target,
    out FP64 distSq, out FP64 rangeSq) {
    distSq = FP64.Zero;
    rangeSq = FP64.Zero;
    if (!frame.Has<TransformComponent>(attacker) || !frame.Has<TransformComponent>(target))
      return false;

    ref readonly var attackerTransform = ref frame.GetReadOnly<TransformComponent>(attacker);
    ref readonly var targetTransform = ref frame.GetReadOnly<TransformComponent>(target);

    var toTarget = targetTransform.Position - attackerTransform.Position;
    toTarget.y = FP64.Zero;
    distSq = toTarget.sqrMagnitude;
    rangeSq = ReachSq(ref frame, attacker, target);
    return distSq <= rangeSq;
  }

  public static FP64 GameplayRadiusOf(ref Frame frame, EntityRef entity) {
    return frame.Has<Stats>(entity)
      ? frame.GetReadOnly<Stats>(entity).GameplayRadius
      : FP64.Zero;
  }

  private static FP64 AttackRangeOf(ref Frame frame, EntityRef entity) {
    return frame.Has<Stats>(entity)
      ? frame.GetReadOnly<Stats>(entity).AttackRange
      : FP64.Zero;
  }
}
