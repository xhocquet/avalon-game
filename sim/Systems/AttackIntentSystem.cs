using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Logging;

namespace Meesles.Avalon;

public class AttackIntentSystem : ISystem {
  private readonly UnitIdIndex _unitIdIndex = new();

  public void Update(ref Frame frame) {
    _unitIdIndex.Rebuild(ref frame);

    var filter = frame.Filter<AttackTargetUnitId, Team, TransformComponent>();
    while (filter.Next(out var attacker))
      UpdateAttacker(ref frame, attacker);
  }

  private void UpdateAttacker(ref Frame frame, EntityRef attacker) {
    if (!frame.Has<Combat>(attacker)) {
      LogAttackState(ref frame, attacker, 0, "cleared_no_combat");
      ClearAttackIntent(ref frame, attacker);
      return;
    }

    var targetUnitId = frame.GetReadOnly<AttackTargetUnitId>(attacker).TargetUnitId;
    if (!TryResolveTarget(ref frame, attacker, targetUnitId, out var target)) {
      LogAttackState(ref frame, attacker, targetUnitId, "cleared_invalid_target");
      ClearAttackIntent(ref frame, attacker);
      ClearMoveTarget(ref frame, attacker);
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
    ClearMoveTarget(ref frame, attacker);

    if (wasOutOfRange)
      LogAttackState(ref frame, attacker, targetUnitId, $"in_range distSq={distSq} rangeSq={rangeSq}");
  }

  // Out of range: mobile units walk to the target, immobile turrets drop the intent entirely.
  private static void PursueTarget(ref Frame frame, EntityRef attacker, EntityRef target) {
    ref var combat = ref frame.Get<Combat>(attacker);
    combat.Target = default;

    if (frame.Has<Turret>(attacker)) {
      ClearAttackIntent(ref frame, attacker);
      ClearMoveTarget(ref frame, attacker);
      return;
    }

    SetMoveTarget(ref frame, attacker, frame.GetReadOnly<TransformComponent>(target).Position);
  }

  private bool TryResolveTarget(ref Frame frame, EntityRef attacker, int targetUnitId,
    out EntityRef target) {
    if (!_unitIdIndex.TryGet(targetUnitId, out target))
      return false;

    if (!frame.Has<Team>(target) || !frame.Has<TransformComponent>(target) || !frame.Has<Health>(target))
      return false;

    ref readonly var health = ref frame.GetReadOnly<Health>(target);
    if (health.Current <= 0)
      return false;

    ref readonly var attackerTeam = ref frame.GetReadOnly<Team>(attacker);
    ref readonly var targetTeam = ref frame.GetReadOnly<Team>(target);
    return attackerTeam.TeamId != targetTeam.TeamId;
  }

  private static void ClearAttackIntent(ref Frame frame, EntityRef entity) {
    if (frame.Has<AttackTargetUnitId>(entity))
      frame.Remove<AttackTargetUnitId>(entity);

    if (frame.Has<Combat>(entity)) {
      ref var combat = ref frame.Get<Combat>(entity);
      combat.Target = default;
    }
  }

  private static void SetMoveTarget(ref Frame frame, EntityRef entity, FPVector3 target) {
    target.y = FP64.Zero;
    if (frame.Has<UnitMoveTarget>(entity)) {
      ref var moveTarget = ref frame.Get<UnitMoveTarget>(entity);
      moveTarget.Target = target;
      return;
    }

    frame.Add(entity, new UnitMoveTarget { Target = target });
  }

  private static void ClearMoveTarget(ref Frame frame, EntityRef entity) {
    if (frame.Has<UnitMoveTarget>(entity))
      frame.Remove<UnitMoveTarget>(entity);
  }

  private static void LogAttackState(ref Frame frame, EntityRef attacker, int attackTargetUnitId, string state) {
    if (!TryGetUnitId(ref frame, attacker, out var sourceUnitId))
      sourceUnitId = 0;

    frame.Logger.KDebug(
      $"[Combat] AttackIntent tick={frame.Tick} sourceUnitId={sourceUnitId} targetUnitId={attackTargetUnitId} state={state}");
  }

  private static bool TryGetUnitId(ref Frame frame, EntityRef entity, out int unitId) {
    if (frame.Has<Unit>(entity)) {
      unitId = frame.GetReadOnly<Unit>(entity).UnitId;
      return true;
    }

    unitId = 0;
    return false;
  }
}
