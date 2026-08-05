using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon;

// Burns down skill cooldowns. Casting itself is command-driven and lives in SkillActions; this is the
// only per-tick skill work there is while the effects are stubbed.
public class SkillSystem : ISystem {
  public void Update(ref Frame frame) {
    var filter = frame.Filter<SkillsComponent>();
    while (filter.Next(out var entity))
      frame.Get<SkillsComponent>(entity).TickCooldowns();
  }
}
