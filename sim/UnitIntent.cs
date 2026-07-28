using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

// Set/clear helpers for the two components that carry a unit's current order. Both are
// optional components, so every write is a set-or-add and every clear is a guarded remove —
// the ECS equivalent of a property setter. CommandSystem, AttackIntentSystem and
// RespawnSystem all drive the same pair, and each had its own copy of these four lines.
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
