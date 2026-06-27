using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Assets;
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

        ref var attackTarget = ref frame.Get<AttackTargetUnitId>(attacker);
        if (!TryResolveTarget(ref frame, attacker, attackTarget.TargetUnitId, out var target)) {
          ref readonly var reacquireTransform = ref frame.GetReadOnly<TransformComponent>(attacker);
          ref readonly var reacquireCombat = ref frame.GetReadOnly<Combat>(attacker);
          if (!TryAcquireNearbyTarget(ref frame, attacker, reacquireTransform.Position, reacquireCombat.AttackRange,
              out target, out int reacquiredUnitId)) {
            LogAttackState(ref frame, attacker, attackTarget.TargetUnitId, "cleared_invalid_target");
            ClearAttackIntent(ref frame, attacker);
            continue;
          }

          LogAttackState(ref frame, attacker, attackTarget.TargetUnitId,
            $"reacquired_target newTargetUnitId={reacquiredUnitId}");
          attackTarget.TargetUnitId = reacquiredUnitId;
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

    private static bool TryAcquireNearbyTarget(ref Frame frame, EntityRef attacker, FPVector3 attackerPosition,
        FP64 attackRange, out EntityRef target, out int targetUnitId) {
      target = default;
      targetUnitId = 0;

      ref readonly var attackerTeam = ref frame.GetReadOnly<Team>(attacker);
      FP64 radius = GetReacquireRadius(ref frame, attackRange);
      FP64 radiusSq = radius * radius;
      bool found = false;
      FP64 bestDistSq = FP64.Zero;
      int bestUnitId = 0;

      var filter = frame.Filter<Unit, Team, Health, TransformComponent>();
      while (filter.Next(out var candidate)) {
        if (candidate.Index == attacker.Index)
          continue;

        ref readonly var candidateTeam = ref frame.GetReadOnly<Team>(candidate);
        if (candidateTeam.TeamId == attackerTeam.TeamId)
          continue;

        ref readonly var health = ref frame.GetReadOnly<Health>(candidate);
        if (health.Current <= 0)
          continue;

        ref readonly var transform = ref frame.GetReadOnly<TransformComponent>(candidate);
        FPVector3 toCandidate = transform.Position - attackerPosition;
        toCandidate.y = FP64.Zero;
        FP64 distSq = toCandidate.sqrMagnitude;
        if (distSq > radiusSq)
          continue;

        ref readonly var unit = ref frame.GetReadOnly<Unit>(candidate);
        if (!found || distSq < bestDistSq || (distSq == bestDistSq && unit.UnitId < bestUnitId)) {
          found = true;
          bestDistSq = distSq;
          bestUnitId = unit.UnitId;
          target = candidate;
        }
      }

      targetUnitId = bestUnitId;
      return found;
    }

    private static FP64 GetReacquireRadius(ref Frame frame, FP64 attackRange) {
      var stats = frame.AssetRegistry.Get<MinionStatsAsset>();
      FP64 multiplier = stats != null && stats.AttackReacquireRangeMultiplier > FP64.Zero
        ? stats.AttackReacquireRangeMultiplier
        : FP64.FromInt(3);
      return attackRange * multiplier;
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
