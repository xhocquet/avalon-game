using Meesles.Avalon.Sim.Assets;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Heroes;

public static class SharedSkillStubs {
  public static void RankGained(ref Frame frame, EntityRef entity, SkillAsset skill, int newRank) { }

  public static void CastHardHit(ref Frame frame, EntityRef entity, SkillAsset skill, int rank) { }

  public static void CastBuff(ref Frame frame, EntityRef entity, SkillAsset skill, int rank) { }

  public static void CastRangeShot(ref Frame frame, EntityRef entity, SkillAsset skill, int rank) { }

  public static void CastUltimate(ref Frame frame, EntityRef entity, SkillAsset skill, int rank) { }
}
