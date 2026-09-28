using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

public static class TrailController {
  public static bool Arm(ref Frame frame, EntityRef caster, SkillAsset skill, int rank) {
    if (skill == null || rank <= 0 || !skill.HasTrail)
      return false;

    var lifetimeTicks = TickMath.MsToTicksCeil(ref frame, skill.TrailDurationMsAtRank(rank));
    if (lifetimeTicks <= 0)
      return false;

    var intervalTicks = TickMath.MsToTicksCeil(ref frame, skill.TrailSegmentIntervalMs);
    if (intervalTicks < 1)
      intervalTicks = 1;

    if (!frame.Has<TrailEmitter>(caster))
      frame.Add(caster, new TrailEmitter());

    ref var emitter = ref frame.Get<TrailEmitter>(caster);
    emitter.SkillAssetId = skill.AssetId;
    emitter.Rank = rank;
    emitter.SegmentsRemaining = skill.TrailSegmentCount;
    emitter.IntervalTicks = intervalTicks;
    emitter.NextDropTick = frame.Tick; // Drop the first segment now.
    emitter.SegmentLifetimeTicks = lifetimeTicks;
    emitter.Width = skill.TrailWidth;
    return true;
  }

  public static void Clear(ref Frame frame, EntityRef entity) {
    if (frame.Has<TrailEmitter>(entity))
      frame.Get<TrailEmitter>(entity).Clear();
  }

  public static bool IsEmitting(ref Frame frame, EntityRef entity) {
    return frame.Has<TrailEmitter>(entity) &&
           frame.GetReadOnly<TrailEmitter>(entity).IsEmitting;
  }
}
