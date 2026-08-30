using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon;

// Converts the XP that ExperienceRewards deposited into levels
// Runs after DeathSystem = same tick processing of hero kill experience
public class ExperienceSystem : ISystem {
  public void Update(ref Frame frame) {
    if (!frame.AssetRegistry.TryGet<XpRulesAsset>(out var rules) || rules.MaxLevel <= 1)
      return;

    var filter = frame.Filter<Hero, Experience, Stats, Health>();
    while (filter.Next(out var entity)) {
      var levelsGained = 0;
      ref var experience = ref frame.Get<Experience>(entity);
      var levelBefore = experience.Level;
      while (experience.Level < rules.MaxLevel &&
             experience.Xp >= rules.TotalXpForLevel(experience.Level + 1)) {
        experience.Level++;
        levelsGained++;
      }

      if (levelsGained == 0)
        continue;

      ApplyLevelGains(ref frame, entity, rules, levelBefore, experience.Level);
      RaiseLevelUpEvent(ref frame, entity, frame.GetReadOnly<Experience>(entity).Level);
    }
  }

  private static void ApplyLevelGains(ref Frame frame, EntityRef entity, XpRulesAsset rules,
    int levelBefore, int levelAfter) {
    var heroAsset = frame.AssetRegistry.Get<HeroAsset>(frame.GetReadOnly<Hero>(entity).HeroAssetId);
    if (heroAsset != null)
      ApplyGrowth(ref frame, entity, heroAsset, rules, levelBefore, levelAfter);

    if (frame.Has<Skills>(entity))
      frame.Get<Skills>(entity).SkillPoints += levelAfter - levelBefore;
  }

  // Per-hero growth off the hero's own row, applied as the difference between the two levels rather
  // than a flat step each - the curve is not linear, and several levels can land on one tick.
  private static void ApplyGrowth(ref Frame frame, EntityRef entity, HeroAsset heroAsset,
    XpRulesAsset rules, int levelBefore, int levelAfter) {
    ref var stats = ref frame.Get<Stats>(entity);
    for (var i = 0; i < StatRanges.Count; i++) {
      var stat = (StatType)i;

      // The pool maxes move through their own application so current HP/mana follow the pool up.
      if (stat is StatType.MaxHealth or StatType.MaxMana)
        continue;

      var growth = heroAsset.GrowthOf(stat);
      if (growth != FP64.Zero)
        stats.Add(stat, StatGrowth.Between(rules, growth, levelBefore, levelAfter));
    }

    HealthApplication.GrantMaxHealth(ref frame, entity,
      StatGrowth.Between(rules, heroAsset.GrowthOf(StatType.MaxHealth), levelBefore, levelAfter));
    ManaApplication.GrantMaxMana(ref frame, entity,
      StatGrowth.Between(rules, heroAsset.GrowthOf(StatType.MaxMana), levelBefore, levelAfter));
  }

  private static void RaiseLevelUpEvent(ref Frame frame, EntityRef entity, int level) {
    if (frame.EventRaiser == null)
      return;

    var evt = EventPool.Get<HeroLeveledUpEvent>();
    evt.UnitId = UnitLookup.GetUnitId(ref frame, entity);
    evt.PlayerId = frame.GetReadOnly<Hero>(entity).PlayerId;
    evt.Level = level;
    evt.Position = frame.Has<TransformComponent>(entity)
      ? frame.GetReadOnly<TransformComponent>(entity).Position
      : FPVector3.Zero;
    frame.EventRaiser.RaiseEvent(evt);
  }
}
