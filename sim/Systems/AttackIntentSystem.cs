using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Models;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Logging;

namespace Meesles.Avalon {
  public class AttackIntentSystem : ISystem {
    public void Update(ref Frame frame) {
      var filter = frame.Filter<AttackTargetUnitId, Team, TransformComponent>();
      while (filter.Next(out var attacker)) {
        if (!frame.Has<Combat>(attacker)) {
          LogAttackState(ref frame, attacker, attackTargetUnitId: 0, "cleared_no_combat");
          ClearAttackIntent(ref frame, attacker);
          continue;
        }

        ref readonly var attackTarget = ref frame.GetReadOnly<AttackTargetUnitId>(attacker);
        if (!TryResolveTarget(ref frame, attacker, attackTarget.TargetUnitId, out var target)) {
          LogAttackState(ref frame, attacker, attackTarget.TargetUnitId, "cleared_invalid_target");
          ClearAttackIntent(ref frame, attacker);
          continue;
        }

        ref var combat = ref frame.Get<Combat>(attacker);
        ref readonly var attackerTransform = ref frame.GetReadOnly<TransformComponent>(attacker);
        ref readonly var targetTransform = ref frame.GetReadOnly<TransformComponent>(target);

        FPVector3 toTarget = targetTransform.Position - attackerTransform.Position;
        toTarget.y = FP64.Zero;
        FP64 rangeSq = combat.AttackRange * combat.AttackRange;

        if (toTarget.sqrMagnitude > rangeSq) {
          combat.Target = default;
          SetMoveTarget(ref frame, attacker, targetTransform.Position);
          continue;
        }

        bool wasOutOfRange = !combat.Target.IsValid;
        combat.Target = target;
        ClearMoveTarget(ref frame, attacker);
        if (wasOutOfRange) {
          LogAttackState(ref frame, attacker, attackTarget.TargetUnitId,
            $"in_range distSq={toTarget.sqrMagnitude} rangeSq={rangeSq}");
        }
      }
    }

    private static bool TryResolveTarget(ref Frame frame, EntityRef attacker, int targetUnitId,
        out EntityRef target) {
      if (!UnitLookup.TryGetEntityByUnitId(ref frame, targetUnitId, out target))
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
      if (!TryGetUnitId(ref frame, attacker, out int sourceUnitId))
        sourceUnitId = 0;

      frame.Logger?.KDebug(
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
}
