using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Heroes;

public sealed class CrystalGiantSkills : HeroSkillSetBase {
  public CrystalGiantSkills()
    : base(CastSpikyPunch, CastHarden, CastCrystalBullets, CastChrysalis) { }

  // (crystal-giant.json)[../../../client/Sim/Data/Assets/heroes/crystal-giant.json:53]
  private static void CastSpikyPunch(ref Frame frame, in SkillCastContext ctx) {
    var skill = ctx.Skill;
    EmpoweredAttackController.Arm(ref frame, ctx.Caster, skill.AssetId,
      skill.ProcDamageMultiplierAtRank(ctx.Rank),
      TickMath.MsToTicksCeil(ref frame, skill.ProcDurationMs),
      skill.ProcResetsAttackCooldown);
  }

  // (crystal-giant.json)[../../../client/Sim/Data/Assets/heroes/crystal-giant.json:65]
  private static void CastHarden(ref Frame frame, in SkillCastContext ctx) {
    BuffsController.ApplySkill(ref frame, ctx.Caster, ctx.Skill, ctx.Rank);
  }

  // (crystal-giant.json)[../../../client/Sim/Data/Assets/heroes/crystal-giant.json:82]
  private static void CastCrystalBullets(ref Frame frame, in SkillCastContext ctx) {
    var skill = ctx.Skill;
    var direction = SkillAim.Direction(ref frame, ctx.Caster, ctx.CasterPosition, ctx.TargetPosition);
    SkillProjectiles.SpawnVolley(ref frame, in ctx, direction,
      skill.ProjectileCount, skill.ProjectileSpacing, skill.ProjectileSpeed, skill.ProjectileRange,
      skill.ProjectileRadius, skill.ProjectileSpawnOffset, skill.DamageAtRank(ctx.Rank));
  }

  // (crystal-giant.json)[../../../client/Sim/Data/Assets/heroes/crystal-giant.json:101]
  private static void CastChrysalis(ref Frame frame, in SkillCastContext ctx) {
    var skill = ctx.Skill;
    var chargeTicks = TickMath.MsToTicksCeil(ref frame, skill.ChargeDurationMs);

    BuffsController.ApplySkill(ref frame, ctx.Caster, ctx.Skill, ctx.Rank);

    if (skill.ChargeRootsCaster)
      SnareController.Apply(ref frame, ctx.Caster, skill.AssetId, chargeTicks);

    SkillCharges.Arm(ref frame, ctx.Caster, skill.AssetId, chargeTicks,
      skill.DamageAtRank(ctx.Rank), skill.AreaRadius,
      TickMath.MsToTicksCeil(ref frame, skill.SnareDurationMsAtRank(ctx.Rank)));
  }
}
