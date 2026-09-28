using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

public static class BurstAttacksController {
  public static bool Queue(ref Frame frame, EntityRef entity, int sourceId, int totalAttacks,
    int delayTicks, int durationTicks, bool resetAttackCooldown = false) {
    if (sourceId == 0 || totalAttacks <= 1 || delayTicks <= 0 || durationTicks <= 0)
      return false;

    if (!frame.Has<AttackBurst>(entity))
      frame.Add(entity, new AttackBurst());

    ref var burst = ref frame.Get<AttackBurst>(entity);
    burst.SourceId = sourceId;
    burst.Remaining = totalAttacks - 1;
    burst.DelayTicks = delayTicks;
    burst.ExpiryTick = frame.Tick + durationTicks;

    if (resetAttackCooldown && frame.Has<Combat>(entity))
      frame.Get<Combat>(entity).CooldownRemainingTicks = 0;

    return true;
  }

  public static int NextCooldownTicks(ref Frame frame, EntityRef attacker, int defaultTicks) {
    if (!frame.Has<AttackBurst>(attacker))
      return defaultTicks;

    ref var burst = ref frame.Get<AttackBurst>(attacker);
    if (!burst.IsQueued)
      return defaultTicks;

    var delayTicks = burst.DelayTicks;
    burst.Remaining--;
    if (burst.Remaining <= 0)
      burst.Clear();

    // Never lengthen the normal attack period
    return delayTicks < defaultTicks ? delayTicks : defaultTicks;
  }

  public static void Clear(ref Frame frame, EntityRef entity) {
    if (frame.Has<AttackBurst>(entity))
      frame.Get<AttackBurst>(entity).Clear();
  }
}
