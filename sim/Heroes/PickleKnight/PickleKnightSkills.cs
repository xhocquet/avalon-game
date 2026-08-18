using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Heroes;

public sealed class PickleKnightSkills : HeroSkillSetBase {
  public PickleKnightSkills()
    : base(CastSlipNSlide, CastDoubleDip, CastRefresh, CastExploosion) { }

  private static void CastSlipNSlide(ref Frame frame, in SkillCastContext ctx) { }

  private static void CastDoubleDip(ref Frame frame, in SkillCastContext ctx) { }

  // Self-cast: restores the row's percentage of the caster's own max health, and cleanses.
  //
  // TODO: the cleanse half is not written yet because nothing applies a negative status - StatBuffs
  // only ever go on as bonuses and there are no stuns, slows, or damage-over-time effects to clear.
  // Once one exists, it comes off here.
  private static void CastRefresh(ref Frame frame, in SkillCastContext ctx) {
    var maxHealth = HealthApplication.GetMaxHealth(ref frame, ctx.Caster);
    HealthApplication.ApplyHeal(ref frame, ctx.Caster,
      maxHealth * ctx.Skill.HealPercentAtRank(ctx.Rank));
  }

  private static void CastExploosion(ref Frame frame, in SkillCastContext ctx) { }
}
