using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Heroes;

// Shared timed-buff application: every entry in the row's BuffStats block goes on one unit at the
// row's rank, holding for BuffDurationMs (+PerRank). The caller picks the unit - the caster for a
// self-buff, a caught target for a debuff.
public static class SkillBuffs {
  public static void Apply(ref Frame frame, in SkillCastContext ctx, EntityRef target) {
    var skill = ctx.Skill;
    var durationTicks = TickMath.MsToTicksCeil(ref frame, skill.BuffDurationMsAtRank(ctx.Rank));

    foreach (var spec in skill.BuffSpecs)
      StatBuffApplication.ApplySpec(ref frame, target, skill.AssetId, spec, ctx.Rank, durationTicks);
  }
}
