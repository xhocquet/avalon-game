using Meesles.Avalon.Sim.Assets;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Heroes;

public sealed class HairyWizardSkills : IHeroSkillSet {
  public void OnRankGained(ref Frame frame, EntityRef entity, int slot, SkillAsset skill, int newRank) { }

  public void OnCast(ref Frame frame, in SkillCastContext ctx) {
    switch ((SkillSlot)ctx.Slot) {
      case SkillSlot.Primary:
        CastHairball(ref frame, in ctx);
        break;
      case SkillSlot.Secondary:
        CastStrangle(ref frame, in ctx);
        break;
      case SkillSlot.Tertiary:
        CastCloseShave(ref frame, in ctx);
        break;
      case SkillSlot.Ultimate:
        CastBadHairDay(ref frame, in ctx);
        break;
    }
  }

  private static void CastHairball(ref Frame frame, in SkillCastContext ctx) { }

  private static void CastStrangle(ref Frame frame, in SkillCastContext ctx) { }

  private static void CastCloseShave(ref Frame frame, in SkillCastContext ctx) { }

  private static void CastBadHairDay(ref Frame frame, in SkillCastContext ctx) { }
}
