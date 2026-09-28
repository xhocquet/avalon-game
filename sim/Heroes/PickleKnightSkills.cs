using System.Collections.Generic;
using Meesles.Avalon.Sim.Assets;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Heroes;

public sealed class PickleKnightSkills()
  : HeroSkillSetBase(CastSlipNSlide, CastDoubleDip, CastRefresh, CastExploosion) {
  // (pickle-knight.json)[../../client/Sim/Data/Assets/heroes/pickle-knight.json:57]
  private static void CastSlipNSlide(ref Frame f, in SkillCastContext ctx) {
    var skill = ctx.Skill;
    var direction = SkillAim.Direction(ref f, ctx.Caster, ctx.CasterPosition, ctx.TargetPosition);
    var healAmount = SkillAsset.AtRank(skill.HealAmount, skill.HealAmountPerRank, ctx.Rank);

    SkillDashes.Start(ref f, ctx.Caster, ctx.CasterPosition, direction,
      skill.DashDistance, skill.DashSpeed, skill.AssetId, ctx.Rank, healAmount);

    BuffsController.ApplySkill(ref f, ctx.Caster, ctx.Skill, ctx.Rank);
  }

  // (pickle-knight.json)[../../client/Sim/Data/Assets/heroes/pickle-knight.json:69]
  private static void CastDoubleDip(ref Frame f, in SkillCastContext ctx) {
    var skill = ctx.Skill;
    BurstAttacksController.Queue(ref f, ctx.Caster, skill.AssetId,
      skill.BurstAttackCountAtRank(ctx.Rank),
      TickMath.MsToTicksCeil(ref f, skill.BurstAttackDelayMs),
      TickMath.MsToTicksCeil(ref f, skill.BurstDurationMs),
      skill.BurstResetsAttackCooldown);
  }

  // (pickle-knight.json)[../../client/Sim/Data/Assets/heroes/pickle-knight.json:79]
  private static void CastRefresh(ref Frame f, in SkillCastContext ctx) {
    var healPercent = SkillAsset.AtRank(ctx.Skill.HealPercent, ctx.Skill.HealPercentPerRank, ctx.Rank);
    var maxHealth = HealthController.GetMaxHealth(ref f, ctx.Caster);
    HealthController.ApplyHeal(ref f, ctx.Caster, maxHealth * healPercent);

    if (ctx.Skill.ClearsDebuffs)
      BuffsController.ClearNegative(ref f, ctx.Caster);
  }

  // (pickle-knight.json)[../../client/Sim/Data/Assets/heroes/pickle-knight.json:92]
  private static void CastExploosion(ref Frame f, in SkillCastContext ctx) {
    var skill = ctx.Skill;
    var hits = new List<EntityRef>();

    CombatRange.CollectAlliesInRadius(ref f, ctx.Caster, ctx.CasterPosition, skill.AreaRadius, hits);
    var healFraction = SkillAsset.AtRank(skill.HealPercent, skill.HealPercentPerRank, ctx.Rank);
    foreach (var ally in hits)
      HealthController.ApplyHeal(ref f, ally,
        HealthController.GetMaxHealth(ref f, ally) * healFraction);

    if (!skill.HasSilence)
      return;

    var silenceTicks = TickMath.MsToTicksCeil(ref f, skill.SilenceDurationMsAtRank(ctx.Rank));
    CombatRange.CollectHostilesInRadius(ref f, ctx.Caster, ctx.CasterPosition, skill.AreaRadius, hits);
    foreach (var foe in hits)
      SilenceController.Apply(ref f, foe, skill.AssetId, silenceTicks);
  }
}
