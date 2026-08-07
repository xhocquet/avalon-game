using xpTURN.Klotho.Deterministic.Math;
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
    SkillProjectiles.SpawnVolley(ref frame, in ctx, AimDirection(ref frame, in ctx),
      skill.ProjectileCount, skill.ProjectileSpacing, skill.ProjectileSpeed, skill.ProjectileRange,
      skill.ProjectileRadius, skill.ProjectileSpawnOffset, skill.DamageAtRank(ctx.Rank));
  }

  private static void CastCarbonCompression(ref Frame frame, in SkillCastContext ctx) { }

  // Planar direction from caster to aim point. Aiming at your own feet fires straight ahead rather
  // than firing nowhere - Rotation is the Atan2(x, z) yaw every mover writes.
  private static FPVector3 AimDirection(ref Frame frame, in SkillCastContext ctx) {
    var toTarget = ctx.TargetPosition - ctx.CasterPosition;
    toTarget.y = FP64.Zero;
    if (toTarget.sqrMagnitude > FP64.Zero)
      return toTarget.normalized;

    var yaw = frame.Has<TransformComponent>(ctx.Caster)
      ? frame.GetReadOnly<TransformComponent>(ctx.Caster).Rotation
      : FP64.Zero;
    return new FPVector3(FP64.Sin(yaw), FP64.Zero, FP64.Cos(yaw));
  }
}
