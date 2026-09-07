using System.Collections.Generic;
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

  // Self-cast: restores the row's percentage of the caster's own max health, then cleanses - if the
  // row sets ClearsDebuffs, every negative status the caster is carrying comes off through
  // StatusEffects.ClearNegative.
  private static void CastRefresh(ref Frame frame, in SkillCastContext ctx) {
    var maxHealth = HealthApplication.GetMaxHealth(ref frame, ctx.Caster);
    HealthApplication.ApplyHeal(ref frame, ctx.Caster,
      maxHealth * ctx.Skill.HealPercentAtRank(ctx.Rank));

    if (ctx.Skill.ClearsItsTargetsDebuffs)
      StatusEffects.ClearNegative(ref frame, ctx.Caster);
  }

  // Self-cast burst: heals the row's percentage of max health onto the caster and every ally inside
  // AreaRadius, and silences every hostile the same disc catches for SilenceDurationMs.
  private static void CastExploosion(ref Frame frame, in SkillCastContext ctx) {
    var skill = ctx.Skill;
    var hits = new List<EntityRef>();

    SkillAreas.CollectAllies(ref frame, ctx.Caster, ctx.CasterPosition, skill.AreaRadius, hits);
    var healFraction = skill.HealPercentAtRank(ctx.Rank);
    foreach (var ally in hits)
      HealthApplication.ApplyHeal(ref frame, ally,
        HealthApplication.GetMaxHealth(ref frame, ally) * healFraction);

    if (!skill.HasSilence)
      return;

    var silenceTicks = TickMath.MsToTicksCeil(ref frame, skill.SilenceDurationMsAtRank(ctx.Rank));
    SkillAreas.Collect(ref frame, ctx.Caster, ctx.CasterPosition, skill.AreaRadius, hits);
    foreach (var foe in hits)
      Silences.Apply(ref frame, foe, skill.AssetId, silenceTicks);
  }
}
