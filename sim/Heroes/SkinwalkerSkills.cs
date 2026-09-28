using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Heroes;

public sealed class SkinwalkerSkills()
  : HeroSkillSetBase(CastSprint, CastDailyPractice, CastEatToSurvive, CastDesperation) {
  // (skinwalker.json)[../../client/Sim/Data/Assets/heroes/skinwalker.json:53]
  private static void CastSprint(ref Frame f, in SkillCastContext ctx) {
    BuffsController.ApplySkill(ref f, ctx.Caster, ctx.Skill, ctx.Rank);
  }

  // (skinwalker.json)[../../client/Sim/Data/Assets/heroes/skinwalker.json:67]
  private static void CastDailyPractice(ref Frame f, in SkillCastContext ctx) { }

  // (skinwalker.json)[../../client/Sim/Data/Assets/heroes/skinwalker.json:82]
  private static void CastEatToSurvive(ref Frame f, in SkillCastContext ctx) { }

  // (skinwalker.json)[../../client/Sim/Data/Assets/heroes/skinwalker.json:92]
  private static void CastDesperation(ref Frame f, in SkillCastContext ctx) {
    BuffsController.ApplySkill(ref f, ctx.Caster, ctx.Skill, ctx.Rank);
  }
}
