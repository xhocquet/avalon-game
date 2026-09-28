using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Heroes;

public sealed class SkinwalkerSkills : HeroSkillSetBase {
  public SkinwalkerSkills()
    : base(CastSprint, CastDailyPractice, CastEatToSurvive, CastDesperation) { }

  // (skinwalker.json)[../../../client/Sim/Data/Assets/heroes/skinwalker.json:53]
  private static void CastSprint(ref Frame frame, in SkillCastContext ctx) {
    BuffsController.ApplySkill(ref frame, ctx.Caster, ctx.Skill, ctx.Rank);
  }

  // (skinwalker.json)[../../../client/Sim/Data/Assets/heroes/skinwalker.json:67]
  private static void CastDailyPractice(ref Frame frame, in SkillCastContext ctx) { }

  // (skinwalker.json)[../../../client/Sim/Data/Assets/heroes/skinwalker.json:82]
  private static void CastEatToSurvive(ref Frame frame, in SkillCastContext ctx) { }

  // (skinwalker.json)[../../../client/Sim/Data/Assets/heroes/skinwalker.json:92]
  private static void CastDesperation(ref Frame frame, in SkillCastContext ctx) {
    BuffsController.ApplySkill(ref frame, ctx.Caster, ctx.Skill, ctx.Rank);
  }
}
