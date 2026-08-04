using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Logging;

namespace Meesles.Avalon;

public class AttackIntentSystem : ISystem {
  private readonly UnitLookup.Index _unitIdIndex = new();

  public void Update(ref Frame frame) {
    _unitIdIndex.Rebuild(ref frame);

    var filter = frame.Filter<AttackTargetUnitId, TeamComponent, TransformComponent>();
    while (filter.Next(out var attacker))
      UpdateAttacker(ref frame, attacker);
  }

  private void UpdateAttacker(ref Frame frame, EntityRef attacker) {
    if (!frame.Has<Combat>(attacker)) {
      LogAttackState(ref frame, attacker, 0, "cleared_no_combat");
      UnitIntent.ClearAttackIntent(ref frame, attacker);
      return;
    }

    var targetUnitId = frame.GetReadOnly<AttackTargetUnitId>(attacker).TargetUnitId;
    if (!TryResolveTarget(ref frame, attacker, targetUnitId, out var target)) {
      LogAttackState(ref frame, attacker, targetUnitId, "cleared_invalid_target");
      UnitIntent.ClearAttackIntent(ref frame, attacker);
      UnitIntent.ClearMoveTarget(ref frame, attacker);
      return;
    }

    if (IsWithinAttackRange(ref frame, attacker, target, out var distSq, out var rangeSq))
      EngageTarget(ref frame, attacker, target, targetUnitId, distSq, rangeSq);
    else
      PursueTarget(ref frame, attacker, target);
  }

  private static bool IsWithinAttackRange(ref Frame frame, EntityRef attacker, EntityRef target,
    out FP64 distSq, out FP64 rangeSq) {
    ref readonly var attackerTransform = ref frame.GetReadOnly<TransformComponent>(attacker);
    ref readonly var targetTransform = ref frame.GetReadOnly<TransformComponent>(target);
    ref readonly var combat = ref frame.GetReadOnly<Combat>(attacker);

    var toTarget = targetTransform.Position - attackerTransform.Position;
    toTarget.y = FP64.Zero;
    distSq = toTarget.sqrMagnitude;
    rangeSq = combat.AttackRange * combat.AttackRange;
    return distSq <= rangeSq;
  }

  // In range: lock the target in and stop moving, logging only the out-of-range -> in-range edge.
  private static void EngageTarget(ref Frame frame, EntityRef attacker, EntityRef target,
    int targetUnitId, FP64 distSq, FP64 rangeSq) {
    ref var combat = ref frame.Get<Combat>(attacker);
    var wasOutOfRange = !combat.Target.IsValid;
    combat.Target = target;
    UnitIntent.ClearMoveTarget(ref frame, attacker);

    if (wasOutOfRange)
      LogAttackState(ref frame, attacker, targetUnitId, $"in_range distSq={distSq} rangeSq={rangeSq}");
  }

  // Out of range: mobile units walk to the target, immobile turrets drop the intent entirely.
  private static void PursueTarget(ref Frame frame, EntityRef attacker, EntityRef target) {
    ref var combat = ref frame.Get<Combat>(attacker);
    combat.Target = default;

    if (frame.Has<Turret>(attacker)) {
      UnitIntent.ClearAttackIntent(ref frame, attacker);
      UnitIntent.ClearMoveTarget(ref frame, attacker);
      return;
    }

    SetMoveTarget(ref frame, attacker, frame.GetReadOnly<TransformComponent>(target).Position);
  }

  // Beyond the shared hostility rule this system also needs the target's position, both to measure
  // range and to walk to it.
  private bool TryResolveTarget(ref Frame frame, EntityRef attacker, int targetUnitId,
    out EntityRef target) {
    return _unitIdIndex.TryGet(targetUnitId, out target) &&
           frame.Has<TransformComponent>(target) &&
           CombatTargeting.IsHostileAndAlive(ref frame, attacker, target);
  }

  private static void SetMoveTarget(ref Frame frame, EntityRef entity, FPVector3 target) {
    target.y = FP64.Zero;
    UnitIntent.SetMoveTarget(ref frame, entity, target);
  }

  private static void LogAttackState(ref Frame frame, EntityRef attacker, int attackTargetUnitId, string state) {
    frame.Logger.KDebug(
      $"[Combat] AttackIntent tick={frame.Tick} sourceUnitId={UnitLookup.GetUnitId(ref frame, attacker)} " +
      $"targetUnitId={attackTargetUnitId} state={state}");
  }
}
