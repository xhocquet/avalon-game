using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

// Depends on optional components: UnitMoveTarget, AttackTargetUnitId, Combat
public static class UnitIntent {
  public static void SetMoveTarget(ref Frame frame, EntityRef entity, FPVector3 target) {
    if (frame.Has<UnitMoveTarget>(entity)) {
      ref var moveTarget = ref frame.Get<UnitMoveTarget>(entity);
      moveTarget.Target = target;
      return;
    }

    frame.Add(entity, new UnitMoveTarget { Target = target });
  }

  public static void ClearMoveTarget(ref Frame frame, EntityRef entity) {
    if (frame.Has<UnitMoveTarget>(entity))
      frame.Remove<UnitMoveTarget>(entity);
  }

  // Drops the attack order and the resolved target together: AttackTargetUnitId is the order,
  // Combat.Target is AttackIntentSystem's resolution of it
  public static void ClearAttackIntent(ref Frame frame, EntityRef entity) {
    if (frame.Has<AttackTargetUnitId>(entity))
      frame.Remove<AttackTargetUnitId>(entity);

    if (frame.Has<Combat>(entity)) {
      ref var combat = ref frame.Get<Combat>(entity);
      combat.Target = default;
    }
  }
}
