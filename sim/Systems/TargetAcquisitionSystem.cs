using System.Collections.Generic;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using Meesles.Avalon.Sim.Navigation;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

public class TargetAcquisitionSystem : ISystem {
  private readonly List<EntityRef> _nearbyCandidates = new();

  // Initialized from combat tuning.
  private SpatialHashGrid _candidateGrid;

  public void Update(ref Frame frame) {
    var rules = frame.AssetRegistry.Get<CombatRulesAsset>();

    _candidateGrid ??= new SpatialHashGrid(rules.TargetGridCellSize);
    BuildCandidateGrid(ref frame);

    // AttackIntentSystem handles units that already have an order.
    var filter = frame.FilterWithout<UnitIdentity, Team, Stats, TransformComponent, AttackTargetUnitId>();
    while (filter.Next(out var attacker)) {
      if (!frame.Has<Combat>(attacker) || !CanAcquireTargets(ref frame, attacker))
        continue;

      ref readonly var stats = ref frame.GetReadOnly<Stats>(attacker);
      ref readonly var transform = ref frame.GetReadOnly<TransformComponent>(attacker);

      if (!TryAcquireTarget(ref frame, attacker, transform.Position, stats.AcquisitionRange,
            out var targetUnitId))
        continue;

      UnitIntentController.SetAttackTarget(ref frame, attacker, targetUnitId);
    }
  }

  // Rebuild the target broad phase once per tick.
  private void BuildCandidateGrid(ref Frame frame) {
    _candidateGrid.Clear();

    var filter = frame.Filter<UnitIdentity, Team, Health, TransformComponent>();
    while (filter.Next(out var candidate)) {
      ref readonly var transform = ref frame.GetReadOnly<TransformComponent>(candidate);
      _candidateGrid.Insert(candidate, transform.Position.ToXZ());
    }
  }

  private static bool CanAcquireTargets(ref Frame frame, EntityRef entity) {
    if (frame.Has<UnitMoveTarget>(entity))
      return false;

    if (!frame.Has<Health>(entity) || !frame.GetReadOnly<Health>(entity).IsAlive)
      return false;

    return frame.Has<Minion>(entity) || frame.Has<Hero>(entity) || frame.Has<Turret>(entity);
  }

  private bool TryAcquireTarget(ref Frame frame, EntityRef attacker,
    FPVector3 attackerPosition, FP64 radius, out int targetUnitId) {
    targetUnitId = 0;
    var found = false;
    var bestPriority = int.MaxValue;
    var bestDistanceSq = FP64.MaxValue;
    var bestUnitId = int.MaxValue;

    // Apply priority, team, and health rules after the exact XZ query.
    _candidateGrid.QueryRadius(attackerPosition.ToXZ(), radius, _nearbyCandidates);

    for (var i = 0; i < _nearbyCandidates.Count; i++) {
      var candidate = _nearbyCandidates[i];
      if (candidate == attacker)
        continue;

      var priority = GetTargetPriority(ref frame, candidate);
      if (priority == int.MaxValue)
        continue;

      if (!CombatTargeting.IsHostileAndAlive(ref frame, attacker, candidate))
        continue;

      ref readonly var unit = ref frame.GetReadOnly<UnitIdentity>(candidate);
      ref readonly var candidateTransform = ref frame.GetReadOnly<TransformComponent>(candidate);
      var distanceSq = Planar.DistanceSq(attackerPosition, candidateTransform.Position);

      // Priority, distance, then UnitId for deterministic ties.
      if (!found || IsBetterCandidate(priority, distanceSq, unit.UnitId,
            bestPriority, bestDistanceSq, bestUnitId)) {
        found = true;
        bestPriority = priority;
        bestDistanceSq = distanceSq;
        bestUnitId = unit.UnitId;
        targetUnitId = unit.UnitId;
      }
    }

    return found;
  }

  private static bool IsBetterCandidate(int priority, FP64 distanceSq, int unitId,
    int bestPriority, FP64 bestDistanceSq, int bestUnitId) {
    if (priority != bestPriority)
      return priority < bestPriority;
    if (distanceSq != bestDistanceSq)
      return distanceSq < bestDistanceSq;
    return unitId < bestUnitId;
  }

  private static int GetTargetPriority(ref Frame frame, EntityRef entity) {
    if (frame.Has<Minion>(entity))
      return 0;
    if (frame.Has<Hero>(entity))
      return 1;
    if (frame.Has<Turret>(entity))
      return 2;
    if (frame.Has<Crystal>(entity))
      return 3;
    return int.MaxValue;
  }
}
