using System.Collections.Generic;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

public static class DashController {
  public static bool Start(ref Frame frame, EntityRef caster, FPVector3 start, FPVector3 direction,
    FP64 distance, FP64 speed, int sourceId, int rank, FP64 healAmount, int dashCount = 1,
    FP64 damage = default, FP64 width = default) {
    if (distance <= FP64.Zero || speed <= FP64.Zero || sourceId == 0 || rank <= 0 || dashCount <= 0)
      return false;

    var end = start + direction * distance;
    end.y = start.y;
    UnitIntentController.ClearMoveTarget(ref frame, caster);

    if (!frame.Has<SkillDash>(caster))
      frame.Add(caster, new SkillDash());

    ref var dash = ref frame.Get<SkillDash>(caster);
    if (dash.IsActive)
      return false;

    if (dash.SourceId != sourceId || dash.Rank != rank || dash.RemainingDashes <= 0) {
      dash.SourceId = sourceId;
      dash.Rank = rank;
      dash.RemainingDashes = dashCount;
    }

    if (dash.RemainingDashes <= 0)
      return false;

    dash.StartPosition = start;
    dash.Destination = end;
    dash.Speed = speed;
    dash.HealAmount = healAmount;
    dash.Damage = damage;
    dash.Width = width;
    dash.RemainingDashes--;
    return true;
  }

  public static bool CanContinue(ref Frame frame, EntityRef caster, int sourceId) {
    if (!frame.Has<SkillDash>(caster))
      return false;

    ref readonly var dash = ref frame.GetReadOnly<SkillDash>(caster);
    return !dash.IsActive && dash.SourceId == sourceId && dash.RemainingDashes > 0;
  }

  public static void Complete(ref Frame frame, EntityRef caster, in SkillDash dash,
    List<EntityRef> allies, List<EntityRef> hostiles) {
    CollectAllies(ref frame, caster, dash.StartPosition, dash.Destination, allies);
    foreach (var ally in allies)
      HealthController.ApplyHeal(ref frame, ally, dash.HealAmount);

    if (dash.Damage > FP64.Zero) {
      CollectHostiles(ref frame, caster, dash.StartPosition, dash.Destination, dash.Width, hostiles);
      foreach (var hostile in hostiles)
        DamageController.ApplyDamage(ref frame, caster, hostile, dash.Damage);
    }
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

  private static void CollectHostiles(ref Frame frame, EntityRef caster, FPVector3 start,
    FPVector3 end, FP64 width, List<EntityRef> hostiles) {
    hostiles.Clear();
    if (width < FP64.Zero)
      return;

    var startXZ = start.ToXZ();
    var endXZ = end.ToXZ();
    var segment = endXZ - startXZ;
    var halfWidth = width / FP64.FromInt(2);
    var filter = frame.Filter<UnitIdentity, Team, Health, TransformComponent>();
    while (filter.Next(out var candidate)) {
      if (!CombatTargeting.IsSkillHittable(ref frame, candidate) ||
          !CombatTargeting.IsHostileAndAlive(ref frame, caster, candidate))
        continue;

      var candidatePosition = frame.GetReadOnly<TransformComponent>(candidate).Position.ToXZ();
      var progress = Planar.ClosestPointTravel(startXZ, endXZ, candidatePosition);
      var nearest = startXZ + segment * progress;
      var radius = halfWidth + CombatRange.GameplayRadiusOf(ref frame, candidate);
      if (Planar.DistanceSq(candidatePosition, nearest) <= radius * radius)
        hostiles.Add(candidate);
    }
  }
}
