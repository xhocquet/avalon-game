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

    var filter = frame.Filter<Hero, ExperienceComponent, StatsComponent, Health>();
    while (filter.Next(out var entity)) {
      var levelsGained = 0;
      ref var experience = ref frame.Get<ExperienceComponent>(entity);
      while (experience.Level < rules.MaxLevel &&
             experience.Experience >= rules.TotalXpForLevel(experience.Level + 1)) {
        experience.Level++;
        levelsGained++;
      }

      if (levelsGained == 0)
        continue;

      ApplyLevelGains(ref frame, entity, rules, levelsGained);
      RaiseLevelUpEvent(ref frame, entity, frame.GetReadOnly<ExperienceComponent>(entity).Level);
    }
  }

  private static void ApplyLevelGains(ref Frame frame, EntityRef entity, XpRulesAsset rules, int levelsGained) {
    ref var stats = ref frame.Get<StatsComponent>(entity);
    stats.Add(StatType.Strength, FP64.FromInt(rules.StrengthPerLevel * levelsGained));
    stats.Add(StatType.AttackSpeed, rules.AttackSpeedPerLevel * FP64.FromInt(levelsGained));

    HealthApplication.GrantMaxHealth(ref frame, entity, rules.MaxHealthPerLevel * levelsGained);

    if (frame.Has<SkillsComponent>(entity))
      frame.Get<SkillsComponent>(entity).SkillPoints += levelsGained;
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
