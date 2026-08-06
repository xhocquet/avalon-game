using Meesles.Avalon.Sim.Assets;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Heroes;

public sealed class HairyWizardSkills : IHeroSkillSet {
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
