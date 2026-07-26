using Meesles.Avalon.Sim.Commands;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon;

public class StatsSystem : ISystem, ICommandSystem {
  public void Update(ref Frame frame) { }

  // Resolves how much damage an attack lands. Today this is just the attacker's own attack-damage
  // stat; it's the single hook the whole attack pipeline (DamageSystem) routes through, so armor
  // mitigation and faction strength/weakness multipliers will layer in here later.
  public static int CalculateDamage(ref Frame frame, EntityRef attacker, EntityRef target) {
    var damage = GetAttackDamage(ref frame, attacker);

    // Future: subtract the target's armor, then apply attacker-vs-target matchup multipliers.

    return damage < 0 ? 0 : damage;
  }

  // The attacker's effective attack damage. Every combat entity carries its own Stats component
  // (seeded from its stat asset at spawn, then mutated over the match by items/level), so Strength
  // is the authoritative damage value attacks read.
  private static int GetAttackDamage(ref Frame frame, EntityRef attacker) {
    return frame.Has<Stats>(attacker) ? frame.GetReadOnly<Stats>(attacker).Strength : 0;
  }

  public void OnCommand(ref Frame frame, ICommand command) {
    if (command is ModifyStatCommand modify)
      HandleModifyStatCommand(ref frame, modify);
  }

  private static void HandleModifyStatCommand(ref Frame frame, ModifyStatCommand command) {
    var filter = frame.Filter<Hero, Stats>();
    while (filter.Next(out var entity)) {
      ref readonly var hero = ref frame.GetReadOnly<Hero>(entity);
      if (hero.PlayerId != command.PlayerId) continue;

      ref var stats = ref frame.Get<Stats>(entity);
      switch (command.StatType) {
        case Sim.StatType.Strength:
          stats.Strength += command.Delta;
          break;
      }

      return;
    }
  }
}
