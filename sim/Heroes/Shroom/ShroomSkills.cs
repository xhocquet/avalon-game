using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Heroes;

public sealed class ShroomSkills : HeroSkillSetBase {
  public ShroomSkills()
    : base(CastVenomousSlobber, CastSnailTrail, CastSwivelEyes, CastMolt) { }

  // Cone: one instant spray of venom down the aim line, damaging every enemy hero and minion caught
  // in the wedge. The aim point is clamped to the row's cast range, so a cast past the cone's reach
  // still resolves where the telegraph drew it.
  private static void CastVenomousSlobber(ref Frame frame, in SkillCastContext ctx) {
    var skill = ctx.Skill;
    var direction = SkillAim.Direction(ref frame, ctx.Caster, ctx.CasterPosition, ctx.TargetPosition);
    SkillCones.ApplyDamage(ref frame, in ctx, direction, skill.ConeRange, skill.ConeAngleDegrees,
      skill.DamageAtRank(ctx.Rank));
  }

  private static void CastSnailTrail(ref Frame frame, in SkillCastContext ctx) { }

  private static void CastSwivelEyes(ref Frame frame, in SkillCastContext ctx) { }

  private static void CastMolt(ref Frame frame, in SkillCastContext ctx) { }
}
