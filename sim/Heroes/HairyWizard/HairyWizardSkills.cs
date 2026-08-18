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

  private static void CastStrangle(ref Frame frame, in SkillCastContext ctx) { }

  // Self-buff: raises move speed by the row's percentage of its current value for its duration.
  // Recasting refreshes rather than stacks, and NavigationAgentSystem picks the new speed up the tick
  // it lands - nothing has to re-issue the move order.
  private static void CastCloseShave(ref Frame frame, in SkillCastContext ctx) {
    var skill = ctx.Skill;
    StatBuffApplication.ApplyPercent(ref frame, ctx.Caster, skill.AssetId, StatType.MoveSpeed,
      skill.BuffPercentAtRank(ctx.Rank), TickMath.MsToTicksCeil(ref frame, skill.BuffDurationMs));
  }

  private static void CastBadHairDay(ref Frame frame, in SkillCastContext ctx) { }
}
