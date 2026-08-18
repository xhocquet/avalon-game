using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

// The one place health goes down. Auto-attacks (DamageSystem) and skills both route through here so
// mitigation, the zero floor, kill credit, and the hit event can never drift apart between them.
public static class DamageApplication {
  private static readonly FP64 Hundred = FP64.FromInt(100);
  private static readonly FP64 Two = FP64.FromInt(2);

  // Returns the damage actually dealt after mitigation. `canCrit` is opt-in per source: auto-attacks
  // roll against Stats.CritChance, skill damage does not.
  //
  // `attackHitId` is the id this hit reports itself under. A caller that raises events about the hit
  // before it lands - anything that modifies the damage on the way in, like an attack proc - takes an
  // id from NextHitId first and passes it here so both sides of the story carry the same one.
  // Everything else leaves it 0 and gets an id allocated here.
  public static FP64 ApplyDamage(ref Frame frame, EntityRef source, EntityRef target, FP64 amount,
    DamageType damageType = DamageType.Physical, bool canCrit = false, int attackHitId = 0) {
    var sourceUnitId = UnitLookup.GetUnitId(ref frame, source);
    if (attackHitId == 0)
      attackHitId = NextHitId(ref frame);

    // Godmode still raises the hit so attack VFX and feedback play; only the health write is skipped,
    // which also leaves LastDamagerUnitId alone and keeps kill credit off an attacker who dealt nothing.
    if (Cheats.BlocksDamage(ref frame, target)) {
      RaiseHitEvent(ref frame, source, target, sourceUnitId, FP64.Zero, false, attackHitId);
      return FP64.Zero;
    }

    var isCrit = false;
    var incoming = canCrit
      ? CriticalStrikes.Scale(ref frame, source, sourceUnitId, amount, out isCrit)
      : amount;

    var damage = Mitigate(ref frame, target, incoming, damageType);

    ref var health = ref frame.Get<Health>(target);
    health.Current -= damage;
    if (health.Current < FP64.Zero)
      health.Current = FP64.Zero;

    // Load-bearing, not bookkeeping: DeathSystem and RespawnSystem read this to resolve the killer,
    // and ExperienceRewards.AwardForKill pays out against it. A kill that skips it awards nobody.
    health.LastDamagerUnitId = sourceUnitId;

    MatchStats.RecordDamage(ref frame, source, target, damage);
    RaiseHitEvent(ref frame, source, target, sourceUnitId, damage, isCrit, attackHitId);
    return damage;
  }

  // Resists scale by a fraction rather than subtracting flat, so stacking approaches but never
  // reaches immunity and low-damage attackers stay relevant. Negative resist is the same curve
  // mirrored - it amplifies toward 2x rather than jumping there, and never inverts the sign.
  public static FP64 Mitigate(ref Frame frame, EntityRef target, FP64 damage,
    DamageType damageType = DamageType.Physical) {
    if (damage <= FP64.Zero || !frame.Has<StatsComponent>(target))
      return damage;

    var stats = frame.GetReadOnly<StatsComponent>(target);
    var resist = damageType == DamageType.Magical ? stats.MagicResist : stats.Armor;

    var multiplier = resist >= FP64.Zero
      ? Hundred / (Hundred + resist)
      : Two - Hundred / (Hundred - resist);

    var mitigated = damage * multiplier;
    return mitigated < FP64.One ? FP64.One : mitigated; // Floor at 1 damage
  }

  // Allocated whether or not anything is listening: the counter is frame state, so a peer that raises
  // no events has to burn the same ids as one that does or the two frames stop hashing the same.
  public static int NextHitId(ref Frame frame) {
    return IdCounter<AttackHitIdCounter>.Next(ref frame);
  }

  private static void RaiseHitEvent(ref Frame frame, EntityRef source, EntityRef target,
    int sourceUnitId, FP64 damage, bool isCrit, int attackHitId) {
    if (frame.EventRaiser == null)
      return;

    var evt = EventPool.Get<AttackHitEvent>();
    evt.AttackerUnitId = sourceUnitId;
    evt.TargetUnitId = UnitLookup.GetUnitId(ref frame, target);
    evt.Damage = damage;
    evt.IsCrit = isCrit ? 1 : 0;
    evt.AttackHitId = attackHitId;
    evt.AttackerPosition = frame.Has<TransformComponent>(source)
      ? frame.GetReadOnly<TransformComponent>(source).Position
      : default;
    evt.TargetPosition = frame.Has<TransformComponent>(target)
      ? frame.GetReadOnly<TransformComponent>(target).Position
      : default;
    frame.EventRaiser.RaiseEvent(evt);
  }
}
