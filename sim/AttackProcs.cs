using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

// Empowered auto-attacks: a cast arms one, the next attack that lands spends it. The multiplier lands
// on the raw attack damage before mitigation, the same place CriticalStrikes puts its own, so the
// proc is worth the same fraction against any armor value and the two stack multiplicatively.
//
// Only DamageSystem consumes, so a skill can never eat the charge the player is holding for an attack.
public static class AttackProcs {
  // Arms the next attack, replacing whatever was waiting. Returns false when the unit cannot hold a
  // proc or the arming is a no-op.
  public static bool Arm(ref Frame frame, EntityRef entity, int sourceId, FP64 damageMultiplier,
    int durationTicks) {
    if (sourceId == 0 || durationTicks <= 0 || damageMultiplier <= FP64.Zero)
      return false;

    if (!frame.Has<AttackProcComponent>(entity))
      frame.Add(entity, new AttackProcComponent());

    ref var proc = ref frame.Get<AttackProcComponent>(entity);
    proc.SourceId = sourceId;
    proc.DamageMultiplier = damageMultiplier;
    proc.ExpiryTick = frame.Tick + durationTicks;
    return true;
  }

  // Damage after the proc. Spends the charge, so it is called once per landed attack, at the point
  // the attack is committed to, and raises its own AttackProcConsumedEvent under the hit's id rather
  // than reporting itself through the hit event.
  public static FP64 Consume(ref Frame frame, EntityRef attacker, EntityRef target, int attackHitId,
    FP64 damage) {
    if (!frame.Has<AttackProcComponent>(attacker))
      return damage;

    ref var proc = ref frame.Get<AttackProcComponent>(attacker);
    if (!proc.IsArmed)
      return damage;

    var multiplier = proc.DamageMultiplier;
    var sourceId = proc.SourceId;
    proc.Clear();

    RaiseConsumedEvent(ref frame, attacker, target, attackHitId, sourceId, multiplier);
    return damage * multiplier;
  }

  public static void Clear(ref Frame frame, EntityRef entity) {
    if (frame.Has<AttackProcComponent>(entity))
      frame.Get<AttackProcComponent>(entity).Clear();
  }

  public static bool IsArmed(ref Frame frame, EntityRef entity) {
    return frame.Has<AttackProcComponent>(entity) &&
           frame.GetReadOnly<AttackProcComponent>(entity).IsArmed;
  }

  private static void RaiseConsumedEvent(ref Frame frame, EntityRef attacker, EntityRef target,
    int attackHitId, int skillAssetId, FP64 multiplier) {
    if (frame.EventRaiser == null)
      return;

    var evt = EventPool.Get<AttackProcConsumedEvent>();
    evt.AttackHitId = attackHitId;
    evt.AttackerUnitId = UnitLookup.GetUnitId(ref frame, attacker);
    evt.TargetUnitId = UnitLookup.GetUnitId(ref frame, target);
    evt.SkillAssetId = skillAssetId;
    evt.DamageMultiplier = multiplier;
    frame.EventRaiser.RaiseEvent(evt);
  }
}
