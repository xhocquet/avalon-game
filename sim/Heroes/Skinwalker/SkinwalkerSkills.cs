using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Heroes;

public sealed class SkinwalkerSkills : HeroSkillSetBase {
  public SkinwalkerSkills()
    : base(CastSprint, CastDailyPractice, CastEatToSurvive, CastDesperation) { }

  // Self-buff: a MoveSpeed percentage and a flat BonusAttackSpeed step, both off the row's BuffStats
  // block, holding for BuffDurationMs (+PerRank). Recasting refreshes.
  private static void CastSprint(ref Frame frame, in SkillCastContext ctx) {
    SkillBuffs.Apply(ref frame, in ctx, ctx.Caster);
  }

  private static void CastDailyPractice(ref Frame frame, in SkillCastContext ctx) { }

  private static void CastEatToSurvive(ref Frame frame, in SkillCastContext ctx) { }

  // Self-buff: the row trades raised offence for cut defence, every entry keyed to the one cast so a
  // recast refreshes the whole set together. The armor/resist entries are authored negative.
  private static void CastDesperation(ref Frame frame, in SkillCastContext ctx) {
    SkillBuffs.Apply(ref frame, in ctx, ctx.Caster);
  }
}
