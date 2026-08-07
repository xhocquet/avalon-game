using Meesles.Avalon.Sim.Assets;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Heroes;

public sealed class PickleKnightSkills : IHeroSkillSet {
  public void OnRankGained(ref Frame frame, EntityRef entity, int slot, SkillAsset skill, int newRank) { }

  public void OnCast(ref Frame frame, in SkillCastContext ctx) {
    switch ((SkillSlot)ctx.Slot) {
      case SkillSlot.Primary:
        CastSlipNSlide(ref frame, in ctx);
        break;
      case SkillSlot.Secondary:
        CastDoubleDip(ref frame, in ctx);
        break;
      case SkillSlot.Tertiary:
        CastRefresh(ref frame, in ctx);
        break;
      case SkillSlot.Ultimate:
        CastExploosion(ref frame, in ctx);
        break;
    }
  }

  private static void CastSlipNSlide(ref Frame frame, in SkillCastContext ctx) { }

  private static void CastDoubleDip(ref Frame frame, in SkillCastContext ctx) { }

  private static void CastRefresh(ref Frame frame, in SkillCastContext ctx) { }

  private static void CastExploosion(ref Frame frame, in SkillCastContext ctx) { }
}
