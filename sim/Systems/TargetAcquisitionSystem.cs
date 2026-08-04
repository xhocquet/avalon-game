using System.Collections.Generic;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using Meesles.Avalon.Sim.Navigation;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon;

public class TargetAcquisitionSystem : ISystem {
  private readonly List<EntityRef> _nearbyCandidates = new();

  // Built on first use: the cell size lives in CombatRulesAsset, which isn't available at
  // construction time.
  private SpatialHashGrid _candidateGrid;

  public void Update(ref Frame frame) {
    var rules = frame.AssetRegistry.Get<CombatRulesAsset>();

    _candidateGrid ??= new SpatialHashGrid(rules.TargetGridCellSize);
    BuildCandidateGrid(ref frame);

    // Attackers that already hold a target are excluded outright — reacquisition is
    // AttackIntentSystem's job, not this system's.
    var filter = frame.FilterWithout<UnitIdComponent, TeamComponent, Combat, TransformComponent, AttackTargetUnitId>();
    while (filter.Next(out var attacker)) {
      if (!CanAcquireTargets(ref frame, attacker))
        continue;

      ref readonly var combat = ref frame.GetReadOnly<Combat>(attacker);
      ref readonly var transform = ref frame.GetReadOnly<TransformComponent>(attacker);

      if (!TryAcquireTarget(ref frame, attacker, transform.Position, combat.AttackRange, out var targetUnitId))
        continue;

      frame.Add(attacker, new AttackTargetUnitId { TargetUnitId = targetUnitId });
    }
  }

  // Broad-phase: bucket every potential target once per tick so TryAcquireTarget only has to
  // narrow-phase-check the handful of candidates near each attacker instead of every unit on the map.
  private void BuildCandidateGrid(ref Frame frame) {
    _candidateGrid.Clear();

    var filter = frame.Filter<UnitIdComponent, TeamComponent, Health, TransformComponent>();
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
    FPVector3 attackerPosition, FP64 attackRange, out int targetUnitId) {
    targetUnitId = 0;
    var radius = GetAcquisitionRadius(ref frame, attacker, attackRange);
    var found = false;
    var bestPriority = int.MaxValue;
    var bestUnitId = int.MaxValue;

    // Grid already narrowed candidates to those within radius (exact XZ distance filtered);
    // remaining checks are the cheap priority/team/health rules the broad-phase can't apply.
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

      ref readonly var unit = ref frame.GetReadOnly<UnitIdComponent>(candidate);
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
    return attackRange * stats.AttackReacquireRangeMultiplier;
  }
}
