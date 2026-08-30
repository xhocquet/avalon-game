using System.Collections.Generic;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Heroes;

public sealed class SnailheadSkills : HeroSkillSetBase {
  public SnailheadSkills()
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

  // Self-cast area buff: the row's defensive stats go on the caster and every allied hero and minion
  // inside AreaRadius, each keyed to this cast so a recast refreshes rather than stacks.
  private static void CastSwivelEyes(ref Frame frame, in SkillCastContext ctx) {
    var hits = new List<EntityRef>();
    SkillAreas.CollectAllies(ref frame, ctx.Caster, ctx.CasterPosition, ctx.Skill.AreaRadius, hits);

    foreach (var ally in hits)
      SkillBuffs.Apply(ref frame, in ctx, ally);
  }

  private static void CastMolt(ref Frame frame, in SkillCastContext ctx) { }
}
