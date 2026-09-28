using System.Collections.Generic;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Heroes;

public sealed class SnailheadSkills()
  : HeroSkillSetBase(CastVenomousSlobber, CastSnailTrail, CastSwivelEyes, CastMolt) {
  // (snailhead.json)[../../client/Sim/Data/Assets/heroes/snailhead.json:54]
  private static void CastVenomousSlobber(ref Frame f, in SkillCastContext ctx) {
    var skill = ctx.Skill;
    var direction = SkillAim.Direction(
      ref f, ctx.Caster, ctx.CasterPosition, ctx.TargetPosition
    );
    DamageController.ApplyConeDamage(
      ref f, ctx.Caster, ctx.CasterPosition, direction, skill.ConeRange,
      skill.ConeAngleDegrees, skill.DamageAtRank(ctx.Rank)
    );
  }

  // (snailhead.json)[../../client/Sim/Data/Assets/heroes/snailhead.json:69]
  private static void CastSnailTrail(ref Frame f, in SkillCastContext ctx) {
    SkillTrails.Arm(ref f, ctx.Caster, ctx.Skill, ctx.Rank);
  }

  // (snailhead.json)[../../client/Sim/Data/Assets/heroes/snailhead.json:83]
  private static void CastSwivelEyes(ref Frame f, in SkillCastContext ctx) {
    var hits = new List<EntityRef>();
    CombatRange.CollectAlliesInRadius(ref f, ctx.Caster, ctx.CasterPosition, ctx.Skill.AreaRadius, hits);

    foreach (var ally in hits)
      BuffsController.ApplySkill(ref f, ally, ctx.Skill, ctx.Rank);
  }

  // (snailhead.json)[../../client/Sim/Data/Assets/heroes/snailhead.json:101]
  private static void CastMolt(ref Frame f, in SkillCastContext ctx) {
    SkillChannels.Arm(ref f, ctx.Caster, ctx.Skill, ctx.Rank, ctx.CasterPosition);
  }

  // TODO: Route channel completion to its owning skill set.
  public override void OnChannelComplete(
    ref Frame f, EntityRef caster, int skillAssetId, int rank) {
    if (skillAssetId != AssetIds.SkillSnailheadUltimate || rank <= 0 ||
        !f.AssetRegistry.TryGet<SkillAsset>(skillAssetId, out var skill))
      return;

    var position = f.GetReadOnly<TransformComponent>(caster).Position;
    var playerId = f.GetReadOnly<Hero>(caster).PlayerId;
    var ctx = new SkillCastContext(
      caster, playerId, (int)SkillSlot.Ultimate, skill, rank, position, position
    );

    HealthController.ApplyHeal(ref f, caster,
      HealthController.GetMaxHealth(ref f, caster) *
      SkillAsset.AtRank(skill.HealPercent, skill.HealPercentPerRank, rank));

    if (skill.ClearsDebuffs)
      BuffsController.ClearNegative(ref f, caster);

    BuffsController.ApplySkill(ref f, caster, ctx.Skill, ctx.Rank);
  }
}
