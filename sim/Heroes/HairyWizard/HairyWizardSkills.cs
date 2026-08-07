using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Heroes;

public sealed class HairyWizardSkills : HeroSkillSetBase {
  public HairyWizardSkills()
    : base(CastHairball, CastStrangle, CastCloseShave, CastBadHairDay) { }

  private static void CastHairball(ref Frame frame, in SkillCastContext ctx) { }

  private static void CastStrangle(ref Frame frame, in SkillCastContext ctx) { }

  private static void CastCloseShave(ref Frame frame, in SkillCastContext ctx) { }

  private static void CastBadHairDay(ref Frame frame, in SkillCastContext ctx) { }
}
