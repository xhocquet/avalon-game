using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Heroes;

public sealed class CrystalGiantSkills : HeroSkillSetBase {
  public CrystalGiantSkills()
    : base(CastSpikyPunch, CastHarden, CastCrystalBullets, CastCarbonCompression) { }

  // Arms the next auto-attack with the row's multiplier and resets the swing timer, so the punch goes
  // out at once rather than waiting out the auto before it. The charge otherwise waits its duration
  // and is spent by the first attack that lands, or lapses unused.
  private static void CastSpikyPunch(ref Frame frame, in SkillCastContext ctx) {
    var skill = ctx.Skill;
    AttackProcs.Arm(ref frame, ctx.Caster, skill.AssetId,
      skill.ProcDamageMultiplierAtRank(ctx.Rank),
      TickMath.MsToTicksCeil(ref frame, skill.ProcDurationMs),
      skill.ProcResetsAttackCooldown != 0);
  }

  // Self-buff: raises both resists by the row's percentage of their current value for its duration.
  // Recasting refreshes rather than stacks - StatBuffApplication keys entries by (skill, stat).
  private static void CastHarden(ref Frame frame, in SkillCastContext ctx) {
    var skill = ctx.Skill;
    var percent = skill.BuffPercentAtRank(ctx.Rank);
    var durationTicks = TickMath.MsToTicksCeil(ref frame, skill.BuffDurationMs);

    StatBuffApplication.ApplyPercent(ref frame, ctx.Caster, skill.AssetId, StatType.Armor, percent,
      durationTicks);
    StatBuffApplication.ApplyPercent(ref frame, ctx.Caster, skill.AssetId, StatType.MagicResist,
      percent, durationTicks);
  }

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
