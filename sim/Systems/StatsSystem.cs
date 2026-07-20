using Meesles.Avalon.Sim.Commands;
using Meesles.Avalon.Sim.Models;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon;

public class StatsSystem : ISystem, ICommandSystem {
  public void Update(ref Frame frame) { }

  public static int CalculateDamage(ref Frame frame, EntityRef attacker, EntityRef target) {
    return 10;
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
