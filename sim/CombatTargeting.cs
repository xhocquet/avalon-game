using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

public static class CombatTargeting {
  public static bool IsHostileAndAlive(ref Frame frame, EntityRef attacker, EntityRef target) {
    return frame.Has<Team>(attacker) &&
           IsHostileAndAlive(ref frame, frame.GetReadOnly<Team>(attacker).TeamId, target);
  }

  // Team-id overload: a projectile can outlive the caster entity it was fired from
  public static bool IsHostileAndAlive(ref Frame frame, int teamId, EntityRef target) {
    if (!target.IsValid || !frame.Has<Health>(target) || !frame.Has<Team>(target))
      return false;

    if (!frame.GetReadOnly<Health>(target).IsAlive)
      return false;

    return frame.GetReadOnly<Team>(target).TeamId != teamId;
  }

  // Same team and still alive. Returns true for the unit itself, so an ally search that walks every
  // unit picks the caster up naturally.
  public static bool IsAlliedAndAlive(ref Frame frame, EntityRef unit, EntityRef target) {
    if (!frame.Has<Team>(unit) || !target.IsValid ||
        !frame.Has<Health>(target) || !frame.Has<Team>(target))
      return false;

    if (!frame.GetReadOnly<Health>(target).IsAlive)
      return false;

    return frame.GetReadOnly<Team>(target).TeamId ==
           frame.GetReadOnly<Team>(unit).TeamId;
  }

  // Structures are excluded from skill hits; a skill that wants one checks past this
  public static bool IsSkillHittable(ref Frame frame, EntityRef target) {
    return frame.Has<Hero>(target) || frame.Has<Minion>(target);
  }
}
