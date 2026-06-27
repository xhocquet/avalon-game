using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Models;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Logging;

namespace Meesles.Avalon {
  public class SpatialIndexSystem : ISystem {
    public void Update(ref Frame frame) { }
  }

  public class TargetAcquisitionSystem : ISystem {
    public void Update(ref Frame frame) { }
  }

  public class PathRequestSystem : ISystem {
    public void Update(ref Frame frame) { }
  }

  public class PathfindingSystem : ISystem {
    public void Update(ref Frame frame) { }
  }

  public class PathFollowSystem : ISystem {
    public void Update(ref Frame frame) { }
  }

  public class LocalAvoidanceSystem : ISystem {
    public void Update(ref Frame frame) { }
  }

  public class MovementIntentSystem : ISystem {
    public void Update(ref Frame frame) { }
  }

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

  public class AttackCooldownSystem : ISystem {
    public void Update(ref Frame frame) {
      var filter = frame.Filter<Combat>();
      while (filter.Next(out var entity)) {
        ref var combat = ref frame.Get<Combat>(entity);
        if (combat.CooldownRemainingTicks > 0)
          combat.CooldownRemainingTicks--;
      }
    }
  }

  public class DamageSystem : ISystem {
    public void Update(ref Frame frame) {
      var filter = frame.Filter<Combat, Team, AttackTargetUnitId>();
      while (filter.Next(out var attacker)) {
        ref var combat = ref frame.Get<Combat>(attacker);
        if (combat.CooldownRemainingTicks > 0) {
          if (combat.CooldownRemainingTicks == combat.AttackCooldownTicks - 1 || combat.CooldownRemainingTicks == 1)
            LogDamageState(ref frame, attacker, combat.Target, $"cooldown_blocked cooldown={combat.CooldownRemainingTicks}");
          continue;
        }

        if (!combat.Target.IsValid)
          continue;

        if (!TryGetDamageTarget(ref frame, attacker, combat.Target, out var target)) {
          LogDamageState(ref frame, attacker, combat.Target, "invalid_damage_target");
          combat.Target = default;
          continue;
        }

        ref var health = ref frame.Get<Health>(target);
        int healthBefore = health.Current;
        health.Current -= combat.AttackDamage;
        if (health.Current < 0)
          health.Current = 0;

        combat.CooldownRemainingTicks = combat.AttackCooldownTicks;
        LogDamageState(ref frame, attacker, target,
          $"damage={combat.AttackDamage} health={healthBefore}->{health.Current} cooldown={combat.CooldownRemainingTicks}");
      }
    }

    private static bool TryGetDamageTarget(ref Frame frame, EntityRef attacker, EntityRef target,
        out EntityRef resolvedTarget) {
      resolvedTarget = target;
      if (!target.IsValid || !frame.Has<Health>(target) || !frame.Has<Team>(target))
        return false;

      ref readonly var health = ref frame.GetReadOnly<Health>(target);
      if (health.Current <= 0)
        return false;

      ref readonly var attackerTeam = ref frame.GetReadOnly<Team>(attacker);
      ref readonly var targetTeam = ref frame.GetReadOnly<Team>(target);
      return attackerTeam.TeamId != targetTeam.TeamId;
    }

    private static void LogDamageState(ref Frame frame, EntityRef attacker, EntityRef target, string state) {
      int sourceUnitId = TryGetUnitId(ref frame, attacker, out int source) ? source : 0;
      int targetUnitId = target.IsValid && TryGetUnitId(ref frame, target, out int resolvedTarget) ? resolvedTarget : 0;
      frame.Logger?.KDebug(
        $"[Combat] DamageSystem tick={frame.Tick} sourceUnitId={sourceUnitId} targetUnitId={targetUnitId} state={state}");
    }

    private static bool TryGetUnitId(ref Frame frame, EntityRef entity, out int unitId) {
      if (entity.IsValid && frame.Has<Unit>(entity)) {
        unitId = frame.GetReadOnly<Unit>(entity).UnitId;
        return true;
      }

      unitId = 0;
      return false;
    }
  }

  public class RewardSystem : ISystem {
    public void Update(ref Frame frame) { }
  }
}
