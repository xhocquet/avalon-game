using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon;

// Converts the XP that ExperienceRewards deposited into levels. Runs after DeathSystem so a kill
// lands its level on the same tick it happened.
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
    var maxHealthGain = rules.MaxHealthPerLevel * levelsGained;

    ref var stats = ref frame.Get<StatsComponent>(entity);
    stats.Add(StatType.MaxHealth, FP64.FromInt(maxHealthGain));
    stats.Add(StatType.Strength, FP64.FromInt(rules.StrengthPerLevel * levelsGained));
    stats.Add(StatType.AttackSpeed, rules.AttackSpeedPerLevel * FP64.FromInt(levelsGained));

    // A bigger pool would otherwise read as a heal debt. Skip a hero waiting on a respawn: it is at
    // zero HP on purpose, and topping it up would read as alive to everything that checks Current.
    ref var health = ref frame.Get<Health>(entity);
    if (health.IsAlive)
      health.Current += maxHealthGain;
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
