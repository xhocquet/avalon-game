using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Heroes;

public sealed class PickleKnightSkills : HeroSkillSetBase {
  public PickleKnightSkills()
    : base(CastSlipNSlide, CastDoubleDip, CastRefresh, CastExploosion) { }

  private static void CastSlipNSlide(ref Frame frame, in SkillCastContext ctx) { }

  // Queues the row's burst of auto-attacks and resets the swing timer, so the swings go out back to
  // back at the burst's spacing rather than at the caster's attack rate. Each one is a plain attack;
  // the burst lapses unspent if nothing is in reach before its duration is up.
  private static void CastDoubleDip(ref Frame frame, in SkillCastContext ctx) {
    var skill = ctx.Skill;
    AttackBursts.Queue(ref frame, ctx.Caster, skill.AssetId,
      skill.BurstAttackCountAtRank(ctx.Rank),
      TickMath.MsToTicksCeil(ref frame, skill.BurstAttackDelayMs),
      TickMath.MsToTicksCeil(ref frame, skill.BurstDurationMs),
      skill.BurstResetsAttackCooldown != 0);
  }

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
