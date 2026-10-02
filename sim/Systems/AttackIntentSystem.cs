using System.Collections.Generic;
using Meesles.Avalon.Sim.Components;
using Meesles.Avalon.Sim.Navigation;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

public class AttackIntentSystem(NavigationRuntime navigation = null) : ISystem {
  private readonly List<EntityRef> _spentIntents = [];
  private readonly UnitLookup.Index _unitIdIndex = new();

  public void Update(ref Frame frame) {
    _unitIdIndex.Rebuild(ref frame);
    _spentIntents.Clear();

    var filter = frame.Filter<AttackTargetUnitId, Team, TransformComponent>();
    while (filter.Next(out var attacker))
      if (!UpdateAttacker(ref frame, attacker))
        _spentIntents.Add(attacker);

    // Deferred: AttackTargetUnitId is one of the filter's own types (see the iteration rule in AGENTS.md).
    for (var i = 0; i < _spentIntents.Count; i++)
      UnitIntentController.ClearAttackIntent(ref frame, _spentIntents[i]);
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
      UnitIntentController.ClearMoveTarget(ref frame, attacker);
      return false;
    }

    if (!CombatRange.IsWithinReach(ref frame, attacker, target, out var distSq, out var rangeSq))
      return PursueTarget(ref frame, attacker, target);

    EngageTarget(ref frame, attacker, targetUnitId, distSq, rangeSq);
    return true;
  }

  // Stop moving and log only the transition into range.
  private static void EngageTarget(ref Frame frame, EntityRef attacker,
    int targetUnitId, FP64 distSq, FP64 rangeSq) {
    ref var combat = ref frame.Get<Combat>(attacker);
    var wasOutOfRange = combat.TargetUnitId == 0;
    combat.TargetUnitId = targetUnitId;
    UnitIntentController.ClearMoveTarget(ref frame, attacker);

    if (wasOutOfRange)
      LogAttackState(ref frame, attacker, targetUnitId, $"in_range distSq={distSq} rangeSq={rangeSq}");
  }

  // Turrets drop out-of-range intents; mobile units pursue.
  private bool PursueTarget(ref Frame frame, EntityRef attacker, EntityRef target) {
    ref var combat = ref frame.Get<Combat>(attacker);
    combat.TargetUnitId = 0;

    if (frame.Has<Turret>(attacker)) {
      UnitIntentController.ClearMoveTarget(ref frame, attacker);
      return false;
    }

    var approach = NavTargets.SnapToWalkable(navigation?.Query,
      frame.GetReadOnly<TransformComponent>(target).Position);
    UnitIntentController.SetMoveTarget(ref frame, attacker, approach);
    return true;
  }

  // Range and pursuit need a target position.
  private bool TryResolveTarget(ref Frame frame, EntityRef attacker, int targetUnitId,
    out EntityRef target) {
    return _unitIdIndex.TryGet(targetUnitId, out target) &&
           frame.Has<TransformComponent>(target) &&
           CombatTargeting.IsHostileAndAlive(ref frame, attacker, target);
  }

  private static void LogAttackState(ref Frame frame, EntityRef attacker, int attackTargetUnitId, string state) {
    SimLog.Debug(ref frame,
      $"[Combat] event=attack_intent tick={frame.Tick} sourceUnitId={UnitLookup.GetUnitId(ref frame, attacker)} " +
      $"targetUnitId={attackTargetUnitId} state={state}");
  }
}
