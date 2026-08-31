using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Heroes;

// The one place a trail emitter goes on a caster or comes off. Arming is the cast-side entry;
// TrailSystem owns the drop cadence, the segment entities, and the per-tick contact test. Kept beside
// SkillCharges and SkillProjectiles as the fourth way a skill puts something in the world.
public static class SkillTrails {
  // Arms an emitter on the caster, replacing any running one. Returns false when the row lays no
  // trail or the numbers resolve to a no-op.
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
    emitter.NextDropTick = frame.Tick; // First circle drops under the caster's feet this tick
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
