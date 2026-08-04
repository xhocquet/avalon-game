using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

// "Is this a living enemy" was written out three times - TargetAcquisitionSystem picking a target,
// AttackIntentSystem holding one, DamageSystem hitting one - which is three chances for the rule to
// drift apart across the phases of a single attack. It lives here once instead.
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
