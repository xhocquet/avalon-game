using System.Collections.Generic;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Heroes;

public static class SkillDashes {
  public static bool Start(ref Frame frame, EntityRef caster, FPVector3 start, FPVector3 direction,
    FP64 distance, FP64 speed, int sourceId, int rank, FP64 healAmount) {
    if (distance <= FP64.Zero || speed <= FP64.Zero || sourceId == 0 || rank <= 0)
      return false;

    var end = start + direction * distance;
    end.y = start.y;
    UnitIntent.ClearMoveTarget(ref frame, caster);

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
      HealthApplication.ApplyHeal(ref frame, ally, dash.HealAmount);
  }

  public static void Clear(ref Frame frame, EntityRef caster) {
    if (frame.Has<SkillDash>(caster))
      frame.Remove<SkillDash>(caster);
  }

  // Returns allies whose bodies cross the path from start to end.
  private static void CollectAllies(ref Frame frame, EntityRef caster, FPVector3 start,
    FPVector3 end, List<EntityRef> allies) {
    allies.Clear();
    var startXZ = start.ToXZ();
    var segment = end.ToXZ() - startXZ;
    var segmentLengthSq = segment.sqrMagnitude;

    var filter = frame.Filter<UnitIdentity, Team, Health, TransformComponent>();
    while (filter.Next(out var candidate)) {
      if (!CombatTargeting.IsSkillHittable(ref frame, candidate) ||
          !CombatTargeting.IsAlliedAndAlive(ref frame, caster, candidate))
        continue;

      var offset = frame.GetReadOnly<TransformComponent>(candidate).Position.ToXZ() - startXZ;
      var progress = FPVector2.Dot(offset, segment) / segmentLengthSq;
      progress = FP64.Clamp(progress, FP64.Zero, FP64.One);
      var nearest = startXZ + segment * progress;
      var body = CombatRange.GameplayRadiusOf(ref frame, candidate);
      if ((offset - (nearest - startXZ)).sqrMagnitude <= body * body)
        allies.Add(candidate);
    }
  }
}
