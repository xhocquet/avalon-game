using System.Collections.Generic;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Heroes;

public sealed class SnailheadSkills : HeroSkillSetBase {
  public SnailheadSkills()
    : base(CastVenomousSlobber, CastSnailTrail, CastSwivelEyes, CastMolt) { }

  // (snailhead.json)[../../../client/Sim/Data/Assets/heroes/snailhead.json:54]
  private static void CastVenomousSlobber(ref Frame frame, in SkillCastContext ctx) {
    var skill = ctx.Skill;
    var direction = SkillAim.Direction(
      ref frame, ctx.Caster, ctx.CasterPosition, ctx.TargetPosition
    );
    SkillCones.ApplyDamage(
      ref frame, in ctx, direction, skill.ConeRange,
      skill.ConeAngleDegrees, skill.DamageAtRank(ctx.Rank)
    );
  }

  // (snailhead.json)[../../../client/Sim/Data/Assets/heroes/snailhead.json:69]
  private static void CastSnailTrail(ref Frame frame, in SkillCastContext ctx) {
    SkillTrails.Arm(ref frame, ctx.Caster, ctx.Skill, ctx.Rank);
  }

  // (snailhead.json)[../../../client/Sim/Data/Assets/heroes/snailhead.json:83]
  private static void CastSwivelEyes(ref Frame frame, in SkillCastContext ctx) {
    var hits = new List<EntityRef>();
    SkillAreas.CollectAllies(ref frame, ctx.Caster, ctx.CasterPosition, ctx.Skill.AreaRadius, hits);

    foreach (var ally in hits)
      SkillBuffs.Apply(ref frame, in ctx, ally);
  }

  // (snailhead.json)[../../../client/Sim/Data/Assets/heroes/snailhead.json:101]
  private static void CastMolt(ref Frame frame, in SkillCastContext ctx) {
    SkillChannels.Arm(ref frame, ctx.Caster, ctx.Skill, ctx.Rank, ctx.CasterPosition);
  }

  public override void OnChannelComplete(ref Frame frame, EntityRef caster, int skillAssetId,
    int rank) {
    if (skillAssetId != AssetIds.SkillSnailheadUltimate || rank <= 0 ||
        !frame.AssetRegistry.TryGet<SkillAsset>(skillAssetId, out var skill))
      return;

    var position = frame.GetReadOnly<TransformComponent>(caster).Position;
    var playerId = frame.GetReadOnly<Hero>(caster).PlayerId;
    var ctx = new SkillCastContext(caster, playerId, (int)SkillSlot.Ultimate, skill, rank,
      position, position);
    HealthApplication.ApplyHeal(ref frame, caster,
      HealthApplication.GetMaxHealth(ref frame, caster) * skill.HealPercentAtRank(rank));

    if (skill.ClearsDebuffs)
      StatusEffects.ClearNegative(ref frame, caster);

    SkillBuffs.Apply(ref frame, in ctx, caster);
  }
}
