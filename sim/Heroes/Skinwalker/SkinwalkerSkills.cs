using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Heroes;

public sealed class SkinwalkerSkills : HeroSkillSetBase {
  public SkinwalkerSkills()
    : base(CastSprint, CastDailyPractice, CastEatToSurvive, CastDesperation) { }

  private static void CastSprint(ref Frame frame, in SkillCastContext ctx) { }

  private static void CastDailyPractice(ref Frame frame, in SkillCastContext ctx) { }

  private static void CastEatToSurvive(ref Frame frame, in SkillCastContext ctx) { }

  private static void CastDesperation(ref Frame frame, in SkillCastContext ctx) { }
}
