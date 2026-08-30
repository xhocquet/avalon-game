using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Heroes;

public sealed class HairyWizardSkills : HeroSkillSetBase {
  public HairyWizardSkills()
    : base(CastHairball, CastStrangle, CastCloseShave, CastBadHairDay) { }

  // Skillshot: one fat hairball rolled toward the aim point, dying on the first enemy hero or minion
  // it touches. Same volley path Crystal Bullets uses, authored down to a single wider bullet.
  private static void CastHairball(ref Frame frame, in SkillCastContext ctx) {
    var skill = ctx.Skill;
    var direction = SkillAim.Direction(ref frame, ctx.Caster, ctx.CasterPosition, ctx.TargetPosition);
    SkillProjectiles.SpawnVolley(ref frame, in ctx, direction,
      skill.ProjectileCount, skill.ProjectileSpacing, skill.ProjectileSpeed, skill.ProjectileRange,
      skill.ProjectileRadius, skill.ProjectileSpawnOffset, skill.DamageAtRank(ctx.Rank));
  }

  // Skillshot with no impact damage: a lasso that lands a MoveSpeed slow and a magic-damage burn on
  // the first enemy it touches. Both come off the row's buff and DoT blocks and are applied by
  // ProjectileSystem when the bullet connects, scaled to the rank it was fired at.
  private static void CastStrangle(ref Frame frame, in SkillCastContext ctx) {
    var skill = ctx.Skill;
    var direction = SkillAim.Direction(ref frame, ctx.Caster, ctx.CasterPosition, ctx.TargetPosition);
    SkillProjectiles.SpawnVolley(ref frame, in ctx, direction,
      count: 1, spacing: FP64.Zero, skill.ProjectileSpeed, skill.ProjectileRange,
      skill.ProjectileRadius, skill.ProjectileSpawnOffset, damage: FP64.Zero);
  }

  // Self-buff: raises move speed by the row's percentage of its current value for its duration.
  // Recasting refreshes rather than stacks, and NavigationAgentSystem picks the new speed up the tick
  // it lands - nothing has to re-issue the move order.
  private static void CastCloseShave(ref Frame frame, in SkillCastContext ctx) {
    SkillBuffs.Apply(ref frame, in ctx, ctx.Caster);
  }

  private static void CastBadHairDay(ref Frame frame, in SkillCastContext ctx) { }
}
