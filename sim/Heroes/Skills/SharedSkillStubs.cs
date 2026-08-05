using Meesles.Avalon.Sim.Assets;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Heroes.Skills;

// Placeholder bodies every hero's skill set currently delegates to. The surrounding machinery is real
// - points are spent, ranks rise, cooldowns burn, events are raised - only the effect is missing.
//
// These are not a base class on purpose. Implementing a skill means replacing one delegation in one
// hero's file with a real body; nothing here is meant to survive as shared behaviour.
public static class SharedSkillStubs {
  public static void RankGained(ref Frame frame, EntityRef entity, SkillAsset skill, int newRank) { }

  public static void CastHardHit(ref Frame frame, EntityRef entity, SkillAsset skill, int rank) { }

  public static void CastBuff(ref Frame frame, EntityRef entity, SkillAsset skill, int rank) { }

  public static void CastRangeShot(ref Frame frame, EntityRef entity, SkillAsset skill, int rank) { }

  public static void CastUltimate(ref Frame frame, EntityRef entity, SkillAsset skill, int rank) { }
}
