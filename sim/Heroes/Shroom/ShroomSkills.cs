using Meesles.Avalon.Sim.Assets;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Heroes;

public sealed class ShroomSkills : IHeroSkillSet {
  public void OnRankGained(ref Frame frame, EntityRef entity, int slot, SkillAsset skill, int newRank) { }

  public void OnCast(ref Frame frame, in SkillCastContext ctx) {
    switch ((SkillSlot)ctx.Slot) {
      case SkillSlot.Primary:
        CastVenomousSlobber(ref frame, in ctx);
        break;
      case SkillSlot.Secondary:
        CastSnailTrail(ref frame, in ctx);
        break;
      case SkillSlot.Tertiary:
        CastSwivelEyes(ref frame, in ctx);
        break;
      case SkillSlot.Ultimate:
        CastMolt(ref frame, in ctx);
        break;
    }
  }

  private static void CastVenomousSlobber(ref Frame frame, in SkillCastContext ctx) { }

  private static void CastSnailTrail(ref Frame frame, in SkillCastContext ctx) { }

  private static void CastSwivelEyes(ref Frame frame, in SkillCastContext ctx) { }

  private static void CastMolt(ref Frame frame, in SkillCastContext ctx) { }
}
