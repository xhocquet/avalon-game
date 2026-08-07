using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Heroes;

public sealed class PickleKnightSkills : HeroSkillSetBase {
  public PickleKnightSkills()
    : base(CastSlipNSlide, CastDoubleDip, CastRefresh, CastExploosion) { }

  private static void CastSlipNSlide(ref Frame frame, in SkillCastContext ctx) { }

  private static void CastDoubleDip(ref Frame frame, in SkillCastContext ctx) { }

  private static void CastRefresh(ref Frame frame, in SkillCastContext ctx) { }

  private static void CastExploosion(ref Frame frame, in SkillCastContext ctx) { }
}
