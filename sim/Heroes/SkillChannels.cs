using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Heroes;

public static class SkillChannels {
  public static bool Arm(ref Frame frame, EntityRef caster, SkillAsset skill, int rank,
    FPVector3 startPosition) {
    if (skill == null || rank <= 0)
      return false;

    var durationTicks = TickMath.MsToTicksCeil(ref frame, skill.ChargeDurationMsAtRank(rank));
    if (durationTicks <= 0)
      return false;

    if (!frame.Has<SkillChannel>(caster))
      frame.Add(caster, new SkillChannel());

    ref var channel = ref frame.Get<SkillChannel>(caster);
    channel.SourceId = skill.AssetId;
    channel.CompleteTick = frame.Tick + durationTicks;
    channel.Rank = rank;
    channel.CancelsOnMove = skill.ChargeCancelsOnMove ? 1 : 0;
    channel.StartPosition = startPosition;
    return true;
  }

  public static bool HasMoved(ref Frame frame, EntityRef caster) {
    if (!frame.Has<SkillChannel>(caster) || !frame.Has<TransformComponent>(caster))
      return false;

    ref readonly var channel = ref frame.GetReadOnly<SkillChannel>(caster);
    return channel.IsActive && channel.HasMoved(frame.GetReadOnly<TransformComponent>(caster).Position);
  }

  public static void Complete(ref Frame frame, EntityRef caster) {
    if (!frame.Has<SkillChannel>(caster) || !frame.Has<Hero>(caster))
      return;

    ref var channel = ref frame.Get<SkillChannel>(caster);
    if (!channel.IsDue(frame.Tick))
      return;

    var sourceId = channel.SourceId;
    var rank = channel.Rank;
    channel.Clear();

    ref readonly var hero = ref frame.GetReadOnly<Hero>(caster);
    if (!frame.AssetRegistry.TryGet<HeroAsset>(hero.HeroAssetId, out var heroAsset))
      return;

    HeroSkillSets.Get(heroAsset.SkillSetId).OnChannelComplete(ref frame, caster, sourceId, rank);
  }

  public static void Clear(ref Frame frame, EntityRef caster) {
    if (frame.Has<SkillChannel>(caster))
      frame.Get<SkillChannel>(caster).Clear();
  }
}
