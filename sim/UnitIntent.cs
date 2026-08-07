using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

// Depends on optional components: UnitMoveTarget, AttackTargetUnitId, Combat
public static class UnitIntent {
  // Movement is planar, so the y is dropped here rather than at each call site: an order aimed at a
  // structure reads its transform straight off the map marker, which carries a height.
  public static void SetMoveTarget(ref Frame frame, EntityRef entity, FPVector3 target) {
    target.y = FP64.Zero;

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

  // Sets the order only. Combat.Target is AttackIntentSystem's resolution of it and is not touched
  // here — it resolves the order against range on the next tick.
  public static void SetAttackTarget(ref Frame frame, EntityRef entity, int targetUnitId) {
    if (frame.Has<AttackTargetUnitId>(entity)) {
      ref var attackTarget = ref frame.Get<AttackTargetUnitId>(entity);
      attackTarget.TargetUnitId = targetUnitId;
      return;
    }

    frame.Add(entity, new AttackTargetUnitId { TargetUnitId = targetUnitId });
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
