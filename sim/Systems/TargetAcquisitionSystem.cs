using System.Collections.Generic;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Models;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon {
  public class TargetAcquisitionSystem : ISystem {
    // On the order of typical acquisition radii (minion AttackRange=4 * multiplier=3, turret range=12),
    // so a query spans roughly a 3x3 cell neighborhood instead of scanning every candidate.
    private static readonly FP64 CandidateGridCellSize = FP64.FromInt(10);

    private readonly Sim.SpatialHashGrid _candidateGrid = new(CandidateGridCellSize);
    private readonly List<EntityRef> _nearbyCandidates = new();

    public void Update(ref Frame frame) {
      BuildCandidateGrid(ref frame);

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

    // Broad-phase: bucket every potential target once per tick so TryAcquireTarget only has to
    // narrow-phase-check the handful of candidates near each attacker instead of every unit on the map.
    private void BuildCandidateGrid(ref Frame frame) {
      _candidateGrid.Clear();

      var filter = frame.Filter<Unit, Team, Health, TransformComponent>();
      while (filter.Next(out var candidate)) {
        ref readonly var transform = ref frame.GetReadOnly<TransformComponent>(candidate);
        _candidateGrid.Insert(candidate, transform.Position.ToXZ());
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

      return frame.Has<Minion>(entity) || frame.Has<Hero>(entity) || frame.Has<Turret>(entity);
    }

    private bool TryAcquireTarget(ref Frame frame, EntityRef attacker, int attackerTeamId,
      FPVector3 attackerPosition, FP64 attackRange, out int targetUnitId) {
      targetUnitId = 0;
      FP64 radius = GetAcquisitionRadius(ref frame, attacker, attackRange);
      bool found = false;
      int bestPriority = int.MaxValue;
      int bestUnitId = int.MaxValue;

      // Grid already narrowed candidates to those within radius (exact XZ distance filtered);
      // remaining checks are the cheap priority/team/health rules the broad-phase can't apply.
      _candidateGrid.QueryRadius(attackerPosition.ToXZ(), radius, _nearbyCandidates);

      for (int i = 0; i < _nearbyCandidates.Count; i++) {
        var candidate = _nearbyCandidates[i];
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

    private static FP64 GetAcquisitionRadius(ref Frame frame, EntityRef attacker, FP64 attackRange) {
      if (frame.Has<Turret>(attacker))
        return attackRange;

      var stats = frame.AssetRegistry.Get<MinionStatsAsset>();
      FP64 multiplier = stats != null && stats.AttackReacquireRangeMultiplier > FP64.Zero
        ? stats.AttackReacquireRangeMultiplier
        : FP64.FromInt(3);
      return attackRange * multiplier;
    }
  }
}
