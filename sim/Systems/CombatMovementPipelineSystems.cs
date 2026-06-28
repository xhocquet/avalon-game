using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Models;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon {
  public class SpatialIndexSystem : ISystem {
    public void Update(ref Frame frame) { }
  }

  public class TargetAcquisitionSystem : ISystem {
    public void Update(ref Frame frame) {
      var filter = frame.Filter<Unit, Team, Combat, TransformComponent>();
      while (filter.Next(out var attacker)) {
        if (!CanAcquireTargets(ref frame, attacker))
          continue;

        ref readonly var team = ref frame.GetReadOnly<Team>(attacker);
        ref readonly var combat = ref frame.GetReadOnly<Combat>(attacker);
        ref readonly var transform = ref frame.GetReadOnly<TransformComponent>(attacker);

        if (!TryAcquireTarget(ref frame, attacker, team.TeamId, transform.Position, combat.AttackRange,
            out int targetUnitId))
          continue;

        frame.Add(attacker, new AttackTargetUnitId { TargetUnitId = targetUnitId });
      }
    }

    private static bool CanAcquireTargets(ref Frame frame, EntityRef entity) {
      if (frame.Has<AttackTargetUnitId>(entity))
        return false;

      if (frame.Has<UnitMoveTarget>(entity))
        return false;

      if (!frame.Has<Health>(entity))
        return false;

      ref readonly var health = ref frame.GetReadOnly<Health>(entity);
      if (health.Current <= 0)
        return false;

      return frame.Has<Minion>(entity) || frame.Has<Hero>(entity);
    }

    private static bool TryAcquireTarget(ref Frame frame, EntityRef attacker, int attackerTeamId,
        FPVector3 attackerPosition, FP64 attackRange, out int targetUnitId) {
      targetUnitId = 0;
      FP64 radius = GetAcquisitionRadius(ref frame, attackRange);
      FP64 radiusSq = radius * radius;
      bool found = false;
      int bestPriority = int.MaxValue;
      int bestUnitId = int.MaxValue;

      var filter = frame.Filter<Unit, Team, Health, TransformComponent>();
      while (filter.Next(out var candidate)) {
        if (candidate.Index == attacker.Index)
          continue;

        int priority = GetTargetPriority(ref frame, candidate);
        if (priority == int.MaxValue)
          continue;

        ref readonly var team = ref frame.GetReadOnly<Team>(candidate);
        if (team.TeamId == attackerTeamId)
          continue;

        ref readonly var health = ref frame.GetReadOnly<Health>(candidate);
        if (health.Current <= 0)
          continue;

        ref readonly var transform = ref frame.GetReadOnly<TransformComponent>(candidate);
        FPVector3 toCandidate = transform.Position - attackerPosition;
        toCandidate.y = FP64.Zero;
        if (toCandidate.sqrMagnitude > radiusSq)
          continue;

        ref readonly var unit = ref frame.GetReadOnly<Unit>(candidate);
        if (!found || priority < bestPriority || (priority == bestPriority && unit.UnitId < bestUnitId)) {
          found = true;
          bestPriority = priority;
          bestUnitId = unit.UnitId;
          targetUnitId = unit.UnitId;
        }
      }

      return found;
    }

    private static int GetTargetPriority(ref Frame frame, EntityRef entity) {
      if (frame.Has<Minion>(entity))
        return 0;
      if (frame.Has<Hero>(entity))
        return 1;
      return int.MaxValue;
    }

    private static FP64 GetAcquisitionRadius(ref Frame frame, FP64 attackRange) {
      var stats = frame.AssetRegistry.Get<MinionStatsAsset>();
      FP64 multiplier = stats != null && stats.AttackReacquireRangeMultiplier > FP64.Zero
        ? stats.AttackReacquireRangeMultiplier
        : FP64.FromInt(3);
      return attackRange * multiplier;
    }
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

  public class RewardSystem : ISystem {
    public void Update(ref Frame frame) { }
  }
}
