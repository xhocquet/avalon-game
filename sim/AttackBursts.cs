using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

// Bursts of auto-attacks: a cast queues the extra swings, each attack that lands spends one and
// shortens the swing timer to the burst's spacing rather than the unit's attack period.
//
// Only DamageSystem spends them, so a skill or a projectile can never eat a swing the player is
// owed. The burst does not touch damage - each swing in it is a plain attack, and an AttackProc
// riding along is spent by the first of them the way it would be by any other attack.
public static class AttackBursts {
  // Queues the swings past the one the caster is about to take, so totalAttacks of 2 owes 1. Returns
  // false when the burst is a no-op. Replaces whatever was queued rather than adding to it.
  //
  // resetAttackCooldown clears the swing timer the caster is sitting on, so the burst opens on the
  // cast tick instead of waiting out the auto before it. Casts run before the Update phase, so the 0
  // is already there when DamageSystem reaches this attacker on the same tick.
  public static bool Queue(ref Frame frame, EntityRef entity, int sourceId, int totalAttacks,
    int delayTicks, int durationTicks, bool resetAttackCooldown = false) {
    if (sourceId == 0 || totalAttacks <= 1 || delayTicks <= 0 || durationTicks <= 0)
      return false;

    if (!frame.Has<AttackBurstComponent>(entity))
      frame.Add(entity, new AttackBurstComponent());

    ref var burst = ref frame.Get<AttackBurstComponent>(entity);
    burst.SourceId = sourceId;
    burst.Remaining = totalAttacks - 1;
    burst.DelayTicks = delayTicks;
    burst.ExpiryTick = frame.Tick + durationTicks;

    if (resetAttackCooldown && frame.Has<Combat>(entity))
      frame.Get<Combat>(entity).CooldownRemainingTicks = 0;

    return true;
  }

  // The swing timer to leave after an attack that just landed. Spends one queued swing, so it is
  // called once per landed attack, at the point the cooldown is set.
  public static int NextCooldownTicks(ref Frame frame, EntityRef attacker, int defaultTicks) {
    if (!frame.Has<AttackBurstComponent>(attacker))
      return defaultTicks;

    ref var burst = ref frame.Get<AttackBurstComponent>(attacker);
    if (!burst.IsQueued)
      return defaultTicks;

    var delayTicks = burst.DelayTicks;
    burst.Remaining--;
    if (burst.Remaining <= 0)
      burst.Clear();

    // A burst spacing longer than the unit's own period would slow it down instead of hurrying it.
    return delayTicks < defaultTicks ? delayTicks : defaultTicks;
  }

  public static void Clear(ref Frame frame, EntityRef entity) {
    if (frame.Has<AttackBurstComponent>(entity))
      frame.Get<AttackBurstComponent>(entity).Clear();
  }

  public static int Remaining(ref Frame frame, EntityRef entity) {
    return frame.Has<AttackBurstComponent>(entity)
      ? frame.GetReadOnly<AttackBurstComponent>(entity).Remaining
      : 0;
  }
}
