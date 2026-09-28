using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Heroes;

public sealed class HairyWizardSkills()
  : HeroSkillSetBase(CastHairball, CastStrangle, CastCloseShave, CastBadHairDay) {

  // (hairy-wizard.json)[../../client/Sim/Data/Assets/heroes/hairy-wizard.json:58]
  private static void CastHairball(ref Frame frame, in SkillCastContext ctx) {
    var skill = ctx.Skill;
    var direction = SkillAim.Direction(ref frame, ctx.Caster, ctx.CasterPosition, ctx.TargetPosition);

    SkillProjectiles.SpawnVolley(ref frame, in ctx, direction,
      skill.ProjectileCount, skill.ProjectileSpacing, skill.ProjectileSpeed, skill.ProjectileRange,
      skill.ProjectileRadius, skill.ProjectileSpawnOffset, skill.DamageAtRank(ctx.Rank));
  }

  // (hairy-wizard.json)[../../client/Sim/Data/Assets/heroes/hairy-wizard.json:77]
  private static void CastStrangle(ref Frame frame, in SkillCastContext ctx) {
    var skill = ctx.Skill;
    var direction = SkillAim.Direction(ref frame, ctx.Caster, ctx.CasterPosition, ctx.TargetPosition);
    // TODO: Read count and spacing from the skill asset.
    SkillProjectiles.SpawnVolley(ref frame, in ctx, direction,
      count: 1, spacing: FP64.Zero, skill.ProjectileSpeed, skill.ProjectileRange,
      skill.ProjectileRadius, skill.ProjectileSpawnOffset, damage: FP64.Zero);
  }

  // (hairy-wizard.json)[../../client/Sim/Data/Assets/heroes/hairy-wizard.json:89]
  private static void CastCloseShave(ref Frame frame, in SkillCastContext ctx) {
    BuffsController.ApplySkill(ref frame, ctx.Caster, ctx.Skill, ctx.Rank);
  }

  // (hairy-wizard.json)[../../client/Sim/Data/Assets/heroes/hairy-wizard.json:102]
  private static void CastBadHairDay(ref Frame frame, in SkillCastContext ctx) {
    var skill = ctx.Skill;
    var chargeTicks = TickMath.MsToTicksCeil(ref frame, skill.ChargeDurationMs);

    SkillCharges.Arm(ref frame, ctx.Caster, skill.AssetId, chargeTicks,
      damage: FP64.Zero, skill.AreaRadius,
      TickMath.MsToTicksCeil(ref frame, skill.SnareDurationMsAtRank(ctx.Rank)),
      skill.DotDamagePerSecondAtRank(ctx.Rank));
  }
}
