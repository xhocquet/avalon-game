using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

public static class CombatTargeting {
  public static bool IsHostileAndAlive(ref Frame frame, EntityRef attacker, EntityRef target) {
    if (!target.IsValid || !frame.Has<Health>(target) || !frame.Has<TeamComponent>(target))
      return false;

    if (!frame.GetReadOnly<Health>(target).IsAlive)
      return false;

    return frame.Has<TeamComponent>(attacker) &&
           frame.GetReadOnly<TeamComponent>(attacker).TeamId != frame.GetReadOnly<TeamComponent>(target).TeamId;
  }
}
