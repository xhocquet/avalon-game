using Meesles.Avalon.Sim.Assets;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Heroes;

public sealed class SkinwalkerSkills : IHeroSkillSet {
  public void OnRankGained(ref Frame frame, EntityRef entity, int slot, SkillAsset skill, int newRank) { }

  public void OnCast(ref Frame frame, in SkillCastContext ctx) {
    switch ((SkillSlot)ctx.Slot) {
      case SkillSlot.Primary:
        CastSprint(ref frame, in ctx);
        break;
      case SkillSlot.Secondary:
        CastDailyPractice(ref frame, in ctx);
        break;
      case SkillSlot.Tertiary:
        CastEatToSurvive(ref frame, in ctx);
        break;
      case SkillSlot.Ultimate:
        CastDesperation(ref frame, in ctx);
        break;
    }
  }

  private static void CastSprint(ref Frame frame, in SkillCastContext ctx) { }

  private static void CastDailyPractice(ref Frame frame, in SkillCastContext ctx) { }

  private static void CastEatToSurvive(ref Frame frame, in SkillCastContext ctx) { }

  private static void CastDesperation(ref Frame frame, in SkillCastContext ctx) { }
}
