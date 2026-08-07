using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Heroes;

public sealed class CrystalGiantSkills : HeroSkillSetBase {
  public CrystalGiantSkills()
    : base(CastSpikyPunch, CastHarden, CastCrystalBullets, CastCarbonCompression) { }

  private static void CastSpikyPunch(ref Frame frame, in SkillCastContext ctx) { }

  private static void CastHarden(ref Frame frame, in SkillCastContext ctx) { }

  // Skillshot: three parallel shards fired abreast toward the aim point,
  // each dying on the first enemy hero or minion it touches.
  private static void CastCrystalBullets(ref Frame frame, in SkillCastContext ctx) {
    var skill = ctx.Skill;
    var direction = SkillAim.Direction(ref frame, ctx.Caster, ctx.CasterPosition, ctx.TargetPosition);
    SkillProjectiles.SpawnVolley(ref frame, in ctx, direction,
      skill.ProjectileCount, skill.ProjectileSpacing, skill.ProjectileSpeed, skill.ProjectileRange,
      skill.ProjectileRadius, skill.ProjectileSpawnOffset, skill.DamageAtRank(ctx.Rank));
  }

  private static void CastCarbonCompression(ref Frame frame, in SkillCastContext ctx) { }
}
