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

  public static FP64 GameplayRadiusOf(ref Frame frame, EntityRef entity) {
    return frame.Has<StatsComponent>(entity)
      ? frame.GetReadOnly<StatsComponent>(entity).GameplayRadius
      : FP64.Zero;
  }

  private static FP64 AttackRangeOf(ref Frame frame, EntityRef entity) {
    return frame.Has<StatsComponent>(entity)
      ? frame.GetReadOnly<StatsComponent>(entity).AttackRange
      : FP64.Zero;
  }
}
