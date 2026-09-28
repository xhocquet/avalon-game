using System.Collections.Generic;
using Meesles.Avalon.Sim.Assets;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Heroes;

public sealed class PickleKnightSkills : HeroSkillSetBase {
  public PickleKnightSkills()
    : base(CastSlipNSlide, CastDoubleDip, CastRefresh, CastExploosion) { }

  // (pickle-knight.json)[../../../client/Sim/Data/Assets/heroes/pickle-knight.json:57]
  private static void CastSlipNSlide(ref Frame frame, in SkillCastContext ctx) {
    var skill = ctx.Skill;
    var direction = SkillAim.Direction(ref frame, ctx.Caster, ctx.CasterPosition, ctx.TargetPosition);
    var healAmount = SkillAsset.AtRank(skill.HealAmount, skill.HealAmountPerRank, ctx.Rank);

    SkillDashes.Start(ref frame, ctx.Caster, ctx.CasterPosition, direction,
      skill.DashDistance, skill.DashSpeed, skill.AssetId, ctx.Rank, healAmount);

    BuffsController.ApplySkill(ref frame, ctx.Caster, ctx.Skill, ctx.Rank);
  }

  // (pickle-knight.json)[../../../client/Sim/Data/Assets/heroes/pickle-knight.json:69]
  private static void CastDoubleDip(ref Frame frame, in SkillCastContext ctx) {
    var skill = ctx.Skill;
    BurstAttacksController.Queue(ref frame, ctx.Caster, skill.AssetId,
      skill.BurstAttackCountAtRank(ctx.Rank),
      TickMath.MsToTicksCeil(ref frame, skill.BurstAttackDelayMs),
      TickMath.MsToTicksCeil(ref frame, skill.BurstDurationMs),
      skill.BurstResetsAttackCooldown);
  }

  // (pickle-knight.json)[../../../client/Sim/Data/Assets/heroes/pickle-knight.json:79]
  private static void CastRefresh(ref Frame frame, in SkillCastContext ctx) {
    var healPercent = SkillAsset.AtRank(ctx.Skill.HealPercent, ctx.Skill.HealPercentPerRank, ctx.Rank);
    var maxHealth = HealthController.GetMaxHealth(ref frame, ctx.Caster);
    HealthController.ApplyHeal(ref frame, ctx.Caster, maxHealth * healPercent);

    if (ctx.Skill.ClearsDebuffs)
      BuffsController.ClearNegative(ref frame, ctx.Caster);
  }

  // (pickle-knight.json)[../../../client/Sim/Data/Assets/heroes/pickle-knight.json:92]
  private static void CastExploosion(ref Frame frame, in SkillCastContext ctx) {
    var skill = ctx.Skill;
    var hits = new List<EntityRef>();

    SkillAreas.CollectAllies(ref frame, ctx.Caster, ctx.CasterPosition, skill.AreaRadius, hits);
    var healFraction = SkillAsset.AtRank(skill.HealPercent, skill.HealPercentPerRank, ctx.Rank);
    foreach (var ally in hits)
      HealthController.ApplyHeal(ref frame, ally,
        HealthController.GetMaxHealth(ref frame, ally) * healFraction);

    if (!skill.HasSilence)
      return;

    var silenceTicks = TickMath.MsToTicksCeil(ref frame, skill.SilenceDurationMsAtRank(ctx.Rank));
    SkillAreas.Collect(ref frame, ctx.Caster, ctx.CasterPosition, skill.AreaRadius, hits);
    foreach (var foe in hits)
      SilenceController.Apply(ref frame, foe, skill.AssetId, silenceTicks);
  }
}
