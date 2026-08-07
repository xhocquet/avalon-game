using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Heroes;

public sealed class ShroomSkills : HeroSkillSetBase {
  public ShroomSkills()
    : base(CastVenomousSlobber, CastSnailTrail, CastSwivelEyes, CastMolt) { }

  private static void CastVenomousSlobber(ref Frame frame, in SkillCastContext ctx) { }

  private static void CastSnailTrail(ref Frame frame, in SkillCastContext ctx) { }

  private static void CastSwivelEyes(ref Frame frame, in SkillCastContext ctx) { }

  private static void CastMolt(ref Frame frame, in SkillCastContext ctx) { }
}
