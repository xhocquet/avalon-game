using Meesles.Avalon.Sim.Assets;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Heroes.Skills;

// The Skinwalker's skill tree. Every slot currently delegates to SharedSkillStubs; implementing one means
// replacing the body of the method below and nothing else.
public sealed class SkinwalkerSkills : IHeroSkillSet {
  public void OnRankGained(ref Frame frame, EntityRef entity, int slot, SkillAsset skill, int newRank) {
    SharedSkillStubs.RankGained(ref frame, entity, skill, newRank);
  }

  public void OnCast(ref Frame frame, EntityRef entity, int slot, SkillAsset skill, int rank) {
    switch ((SkillSlot)slot) {
      case SkillSlot.HardHit:
        CastHardHit(ref frame, entity, skill, rank);
        break;
      case SkillSlot.Buff:
        CastBuff(ref frame, entity, skill, rank);
        break;
      case SkillSlot.RangeShot:
        CastRangeShot(ref frame, entity, skill, rank);
        break;
      case SkillSlot.Ultimate:
        CastUltimate(ref frame, entity, skill, rank);
        break;
    }
  }

  private static void CastHardHit(ref Frame frame, EntityRef entity, SkillAsset skill, int rank) {
    SharedSkillStubs.CastHardHit(ref frame, entity, skill, rank);
  }

  private static void CastBuff(ref Frame frame, EntityRef entity, SkillAsset skill, int rank) {
    SharedSkillStubs.CastBuff(ref frame, entity, skill, rank);
  }

  private static void CastRangeShot(ref Frame frame, EntityRef entity, SkillAsset skill, int rank) {
    SharedSkillStubs.CastRangeShot(ref frame, entity, skill, rank);
  }

  private static void CastUltimate(ref Frame frame, EntityRef entity, SkillAsset skill, int rank) {
    SharedSkillStubs.CastUltimate(ref frame, entity, skill, rank);
  }
}
