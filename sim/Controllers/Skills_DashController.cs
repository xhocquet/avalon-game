using System.Collections.Generic;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

public static class DashController {
  public static bool Start(ref Frame frame, EntityRef caster, FPVector3 start, FPVector3 direction,
    FP64 distance, FP64 speed, int sourceId, int rank, FP64 healAmount) {
    if (distance <= FP64.Zero || speed <= FP64.Zero || sourceId == 0 || rank <= 0)
      return false;

    var end = start + direction * distance;
    end.y = start.y;
    UnitIntentController.ClearMoveTarget(ref frame, caster);

    if (!frame.Has<SkillDash>(caster))
      frame.Add(caster, new SkillDash());

    ref var dash = ref frame.Get<SkillDash>(caster);
    dash.StartPosition = start;
    dash.Destination = end;
    dash.Speed = speed;
    dash.HealAmount = healAmount;
    dash.SourceId = sourceId;
    dash.Rank = rank;
    return true;
  }

  public static void Complete(ref Frame frame, EntityRef caster, in SkillDash dash,
    List<EntityRef> allies) {
    CollectAllies(ref frame, caster, dash.StartPosition, dash.Destination, allies);
    foreach (var ally in allies)
      HealthController.ApplyHeal(ref frame, ally, dash.HealAmount);
  }

  public static void Clear(ref Frame frame, EntityRef caster) {
    if (frame.Has<SkillDash>(caster))
      frame.Remove<SkillDash>(caster);
  }

  // Include bodies intersecting the dash path.
  private static void CollectAllies(ref Frame frame, EntityRef caster, FPVector3 start,
    FPVector3 end, List<EntityRef> allies) {
    allies.Clear();
    var startXZ = start.ToXZ();
    var endXZ = end.ToXZ();
    var segment = endXZ - startXZ;

    var filter = frame.Filter<UnitIdentity, Team, Health, TransformComponent>();
    while (filter.Next(out var candidate)) {
      if (!CombatTargeting.IsSkillHittable(ref frame, candidate) ||
          !CombatTargeting.IsAlliedAndAlive(ref frame, caster, candidate))
        continue;

      var candidatePosition = frame.GetReadOnly<TransformComponent>(candidate).Position.ToXZ();
      var progress = Planar.ClosestPointTravel(startXZ, endXZ, candidatePosition);
      var nearest = startXZ + segment * progress;
      var body = CombatRange.GameplayRadiusOf(ref frame, candidate);
      if (Planar.DistanceSq(candidatePosition, nearest) <= body * body)
        allies.Add(candidate);
    }
  }
}
