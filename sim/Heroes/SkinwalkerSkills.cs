using Meesles.Avalon.Sim.Assets;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Heroes;

public sealed class SkinwalkerSkills()
  : HeroSkillSetBase(CastSprint, CastDailyPractice, CastEatToSurvive, CastDesperation) {
  // (skinwalker.json)[../../client/Sim/Data/Assets/heroes/skinwalker.json:53]
  private static void CastSprint(ref Frame f, in SkillCastContext ctx) {
    BuffsController.ApplySkill(ref f, ctx.Caster, ctx.Skill, ctx.Rank);
  }

  // (skinwalker.json)[../../client/Sim/Data/Assets/heroes/skinwalker.json:67]
  private static void CastDailyPractice(ref Frame f, in SkillCastContext ctx) {
    var skill = ctx.Skill;
    var direction = SkillAim.Direction(ref f, ctx.Caster, ctx.CasterPosition, ctx.TargetPosition);
    DashController.Start(ref f, ctx.Caster, ctx.CasterPosition, direction,
      skill.DashDistance, skill.DashSpeed, skill.AssetId, ctx.Rank, healAmount: default,
      skill.DashCountAtRank(ctx.Rank), skill.DamageAtRank(ctx.Rank), skill.DashWidth);
    BuffsController.ApplySkill(ref f, ctx.Caster, skill, ctx.Rank);
  }

  // (skinwalker.json)[../../client/Sim/Data/Assets/heroes/skinwalker.json:82]
  private static void CastEatToSurvive(ref Frame f, in SkillCastContext ctx) {
    var skill = ctx.Skill;
    HealthController.ApplyHeal(ref f, ctx.Caster,
      SkillAsset.AtRank(skill.HealAmount, skill.HealAmountPerRank, ctx.Rank));
    ManaController.Restore(ref f, ctx.Caster,
      SkillAsset.AtRank(skill.ManaRestore, skill.ManaRestorePerRank, ctx.Rank));
  }

  // (skinwalker.json)[../../client/Sim/Data/Assets/heroes/skinwalker.json:92]
  private static void CastDesperation(ref Frame f, in SkillCastContext ctx) {
    BuffsController.ApplySkill(ref f, ctx.Caster, ctx.Skill, ctx.Rank);
  }
}
