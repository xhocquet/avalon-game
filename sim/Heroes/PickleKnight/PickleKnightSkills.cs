using System.Collections.Generic;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Heroes;

public sealed class PickleKnightSkills : HeroSkillSetBase {
  public PickleKnightSkills()
    : base(CastSlipNSlide, CastDoubleDip, CastRefresh, CastExploosion) { }

  // (pickle-knight.json)[../../../client/Sim/Data/Assets/heroes/pickle-knight.json:57]
  private static void CastSlipNSlide(ref Frame frame, in SkillCastContext ctx) {
    var skill = ctx.Skill;
    var direction = SkillAim.Direction(ref frame, ctx.Caster, ctx.CasterPosition, ctx.TargetPosition);
    SkillDashes.Start(ref frame, ctx.Caster, ctx.CasterPosition, direction, skill.DashDistance,
      skill.DashSpeed, skill.AssetId, ctx.Rank, skill.HealAmountAtRank(ctx.Rank));

    SkillBuffs.Apply(ref frame, in ctx, ctx.Caster);
  }

  // (pickle-knight.json)[../../../client/Sim/Data/Assets/heroes/pickle-knight.json:69]
  private static void CastDoubleDip(ref Frame frame, in SkillCastContext ctx) {
    var skill = ctx.Skill;
    AttackBursts.Queue(ref frame, ctx.Caster, skill.AssetId,
      skill.BurstAttackCountAtRank(ctx.Rank),
      TickMath.MsToTicksCeil(ref frame, skill.BurstAttackDelayMs),
      TickMath.MsToTicksCeil(ref frame, skill.BurstDurationMs),
      skill.BurstResetsAttackCooldown != 0);
  }

  // (pickle-knight.json)[../../../client/Sim/Data/Assets/heroes/pickle-knight.json:79]
  private static void CastRefresh(ref Frame frame, in SkillCastContext ctx) {
    var maxHealth = HealthApplication.GetMaxHealth(ref frame, ctx.Caster);
    HealthApplication.ApplyHeal(ref frame, ctx.Caster,
      maxHealth * ctx.Skill.HealPercentAtRank(ctx.Rank));

    if (ctx.Skill.ClearsItsTargetsDebuffs)
      StatusEffects.ClearNegative(ref frame, ctx.Caster);
  }

  // (pickle-knight.json)[../../../client/Sim/Data/Assets/heroes/pickle-knight.json:92]
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
