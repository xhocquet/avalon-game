using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

public static class EmpoweredAttackController {
  public static bool Arm(ref Frame frame, EntityRef entity, int sourceId, FP64 damageMultiplier,
    int durationTicks, bool resetAttackCooldown = false) {
    if (sourceId == 0 || durationTicks <= 0 || damageMultiplier <= FP64.Zero)
      return false;

    if (!frame.Has<AttackProc>(entity))
      frame.Add(entity, new AttackProc());

    ref var proc = ref frame.Get<AttackProc>(entity);
    proc.SourceId = sourceId;
    proc.DamageMultiplier = damageMultiplier;
    proc.ExpiryTick = frame.Tick + durationTicks;

    if (resetAttackCooldown && frame.Has<Combat>(entity))
      frame.Get<Combat>(entity).CooldownRemainingTicks = 0;

    return true;
  }

  public static FP64 Consume(ref Frame frame, EntityRef attacker, EntityRef target, int attackHitId,
    FP64 damage) {
    if (!frame.Has<AttackProc>(attacker))
      return damage;

    ref var proc = ref frame.Get<AttackProc>(attacker);
    if (!proc.IsArmed)
      return damage;

    var multiplier = proc.DamageMultiplier;
    var sourceId = proc.SourceId;
    proc.Clear();

    RaiseConsumedEvent(ref frame, attacker, target, attackHitId, sourceId, multiplier);
    return damage * multiplier;
  }

  public static void Clear(ref Frame frame, EntityRef entity) {
    if (frame.Has<AttackProc>(entity))
      frame.Get<AttackProc>(entity).Clear();
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
