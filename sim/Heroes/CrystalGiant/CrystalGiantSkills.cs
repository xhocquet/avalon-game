using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Heroes;

public sealed class CrystalGiantSkills : HeroSkillSetBase {
  public CrystalGiantSkills()
    : base(CastSpikyPunch, CastHarden, CastCrystalBullets, CastChrysalis) { }

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

  // Self-buff: every stat the row names goes on the caster for the duration. Recasting refreshes
  // rather than stacks - StatBuffApplication keys entries by (skill, stat).
  private static void CastHarden(ref Frame frame, in SkillCastContext ctx) {
    SkillBuffs.Apply(ref frame, in ctx, ctx.Caster);
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

  // The one channelled skill: the giant crystallises where it stands and the shell goes off when the
  // charge finishes. Three effects on one clock, all off the same row - the armor spike that makes
  // standing still survivable, the root that is what it costs, and the charge that pays out.
  //
  // The burst is centred on the caster at detonation, not at the cast point, so it lands where the
  // giant is; the root is what keeps those the same place. Dying mid-channel takes the charge with it
  // - RespawnSystem.ClearActiveState drops it the same way it drops buffs.
  private static void CastChrysalis(ref Frame frame, in SkillCastContext ctx) {
    var skill = ctx.Skill;
    var chargeTicks = TickMath.MsToTicksCeil(ref frame, skill.ChargeDurationMs);

    SkillBuffs.Apply(ref frame, in ctx, ctx.Caster);

    if (skill.ChargeRootsItsCaster)
      Snares.Apply(ref frame, ctx.Caster, skill.AssetId, chargeTicks);

    SkillCharges.Arm(ref frame, ctx.Caster, skill.AssetId, chargeTicks,
      skill.DamageAtRank(ctx.Rank), skill.AreaRadius,
      TickMath.MsToTicksCeil(ref frame, skill.SnareDurationMsAtRank(ctx.Rank)));
  }
}
