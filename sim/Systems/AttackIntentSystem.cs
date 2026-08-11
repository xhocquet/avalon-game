using System.Collections.Generic;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Components;
using Meesles.Avalon.Sim.Navigation;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Logging;

namespace Meesles.Avalon;

public class AttackIntentSystem(NavigationRuntime navigation = null) : ISystem {
  private readonly List<EntityRef> _spentIntents = [];
  private readonly UnitLookup.Index _unitIdIndex = new();

  public void Update(ref Frame frame) {
    _unitIdIndex.Rebuild(ref frame);
    _spentIntents.Clear();

    var filter = frame.Filter<AttackTargetUnitId, TeamComponent, TransformComponent>();
    while (filter.Next(out var attacker))
      if (!UpdateAttacker(ref frame, attacker))
        _spentIntents.Add(attacker);

    // Deferred: AttackTargetUnitId is one of the filter's own types (see the iteration rule in AGENTS.md).
    for (var i = 0; i < _spentIntents.Count; i++)
      UnitIntent.ClearAttackIntent(ref frame, _spentIntents[i]);
  }

  // False means the order is spent and its AttackTargetUnitId comes off after the loop.
  private bool UpdateAttacker(ref Frame frame, EntityRef attacker) {
    if (!frame.Has<Combat>(attacker)) {
      LogAttackState(ref frame, attacker, 0, "cleared_no_combat");
      return false;
    }

    var targetUnitId = frame.GetReadOnly<AttackTargetUnitId>(attacker).TargetUnitId;
    if (!TryResolveTarget(ref frame, attacker, targetUnitId, out var target)) {
      LogAttackState(ref frame, attacker, targetUnitId, "cleared_invalid_target");
      UnitIntent.ClearMoveTarget(ref frame, attacker);
      return false;
    }

    if (!IsWithinAttackRange(ref frame, attacker, target, out var distSq, out var rangeSq))
      return PursueTarget(ref frame, attacker, target);

    EngageTarget(ref frame, attacker, targetUnitId, distSq, rangeSq);
    return true;
  }

  private static bool IsWithinAttackRange(ref Frame frame, EntityRef attacker, EntityRef target,
    out FP64 distSq, out FP64 rangeSq) {
    ref readonly var attackerTransform = ref frame.GetReadOnly<TransformComponent>(attacker);
    ref readonly var targetTransform = ref frame.GetReadOnly<TransformComponent>(target);

    var range = frame.Has<StatsComponent>(attacker)
      ? frame.GetReadOnly<StatsComponent>(attacker).AttackRange
      : FP64.Zero;

    var toTarget = targetTransform.Position - attackerTransform.Position;
    toTarget.y = FP64.Zero;
    distSq = toTarget.sqrMagnitude;
    rangeSq = range * range;
    return distSq <= rangeSq;
  }

  // In range: lock the target in and stop moving, logging only the out-of-range -> in-range edge.
  private static void EngageTarget(ref Frame frame, EntityRef attacker,
    int targetUnitId, FP64 distSq, FP64 rangeSq) {
    ref var combat = ref frame.Get<Combat>(attacker);
    var wasOutOfRange = combat.TargetUnitId == 0;
    combat.TargetUnitId = targetUnitId;
    UnitIntent.ClearMoveTarget(ref frame, attacker);

    if (wasOutOfRange)
      LogAttackState(ref frame, attacker, targetUnitId, $"in_range distSq={distSq} rangeSq={rangeSq}");
  }

  // Out of range: mobile units walk to the target, immobile turrets drop the intent entirely.
  private bool PursueTarget(ref Frame frame, EntityRef attacker, EntityRef target) {
    ref var combat = ref frame.Get<Combat>(attacker);
    combat.TargetUnitId = 0;

    if (frame.Has<Turret>(attacker)) {
      UnitIntent.ClearMoveTarget(ref frame, attacker);
      return false;
    }

    var approach = NavTargets.SnapToWalkable(navigation?.Query,
      frame.GetReadOnly<TransformComponent>(target).Position);
    UnitIntent.SetMoveTarget(ref frame, attacker, approach);
    return true;
  }

  // Beyond the shared hostility rule this system also needs the target's position, both to measure
  // range and to walk to it.
  private bool TryResolveTarget(ref Frame frame, EntityRef attacker, int targetUnitId,
    out EntityRef target) {
    return _unitIdIndex.TryGet(targetUnitId, out target) &&
           frame.Has<TransformComponent>(target) &&
           CombatTargeting.IsHostileAndAlive(ref frame, attacker, target);
  }

  private static void LogAttackState(ref Frame frame, EntityRef attacker, int attackTargetUnitId, string state) {
    frame.Logger.KDebug(
      $"[Combat] AttackIntent tick={frame.Tick} sourceUnitId={UnitLookup.GetUnitId(ref frame, attacker)} " +
      $"targetUnitId={attackTargetUnitId} state={state}");
  }
}
