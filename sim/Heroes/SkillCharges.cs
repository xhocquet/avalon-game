using System.Collections.Generic;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Heroes;

// Delayed burst lifecycle: a cast arms a charge on its caster, the clock runs it down, and it pays
// out as one disc centred on the caster. The third way a skill can reach a unit, after a projectile
// that travels and a cone that resolves on the cast tick - this one resolves on a later tick, so the
// caster's own position at detonation is what the burst is measured from, not where the cast happened.
//
// The payload rides the component rather than being re-read off the row, so a charge always pays what
// the rank that armed it authored.
public static class SkillCharges {
  // Starts a charge, replacing whatever was on the caster. Returns false when the charge is a no-op.
  // A positive auraDamagePerSecond makes the wind-up a channel: the disc pulses that rate at every
  // hostile inside it on DamageOverTime.PayoutIntervalMs boundaries, with the tail paid at detonation.
  public static bool Arm(ref Frame frame, EntityRef entity, int sourceId, int delayTicks, FP64 damage,
    FP64 radius, int snareDurationTicks, FP64 auraDamagePerSecond = default) {
    if (sourceId == 0 || delayTicks <= 0 || radius <= FP64.Zero)
      return false;

    if (!frame.Has<SkillChargeComponent>(entity))
      frame.Add(entity, new SkillChargeComponent());

    ref var charge = ref frame.Get<SkillChargeComponent>(entity);
    charge.SourceId = sourceId;
    charge.DetonateTick = frame.Tick + delayTicks;
    charge.SnareDurationTicks = snareDurationTicks;
    charge.Damage = damage;
    charge.Radius = radius;
    charge.AuraPending = FP64.Zero;

    if (auraDamagePerSecond > FP64.Zero) {
      var interval = TickMath.MsToTicksCeil(ref frame, DamageOverTime.PayoutIntervalMs);
      if (interval < 1)
        interval = 1;
      charge.AuraIntervalTicks = interval;
      charge.AuraNextPulseTick = frame.Tick + interval;
      charge.AuraAccrualPerTick =
        auraDamagePerSecond * FP64.FromInt(TickMath.DeltaTimeMs(ref frame)) / FP64.FromInt(1000);
    }
    else {
      charge.AuraIntervalTicks = 0;
      charge.AuraNextPulseTick = 0;
      charge.AuraAccrualPerTick = FP64.Zero;
    }

    return true;
  }

  // One tick of the channel aura: accrue, and on a payout boundary re-collect the disc and deal the
  // accrued whole to every hostile in it. TimedEffectSystem calls this each tick a charge is still
  // winding up. Membership is re-read here, so the aura follows a moving caster and a foe walking in
  // or out of the disc between pulses is caught or spared accordingly.
  public static void TickAura(ref Frame frame, EntityRef caster) {
    if (!frame.Has<SkillChargeComponent>(caster))
      return;

    ref var charge = ref frame.Get<SkillChargeComponent>(caster);
    if (!charge.IsCharging || !charge.HasAura)
      return;

    charge.AuraPending += charge.AuraAccrualPerTick;
    if (frame.Tick < charge.AuraNextPulseTick)
      return;

    charge.AuraNextPulseTick += charge.AuraIntervalTicks;
    var whole = FP64.Floor(charge.AuraPending);
    if (whole < FP64.One)
      return;
    charge.AuraPending -= whole;

    var radius = charge.Radius;
    // charge ref released past here: ApplyDamage can allocate the hit-id singleton on its first call.
    PayAuraPulse(ref frame, caster, radius, whole);
  }

  // Pays the charge out and clears it. Damages every hostile hero and minion in the disc and snares
  // each of them for the row's hold; a charge authored no hold only damages.
  public static void Detonate(ref Frame frame, EntityRef caster) {
    if (!frame.Has<SkillChargeComponent>(caster))
      return;

    ref var charge = ref frame.Get<SkillChargeComponent>(caster);
    if (!charge.IsCharging)
      return;

    var sourceId = charge.SourceId;
    // Burst damage plus whatever the channel aura accrued since its last pulse - the tail instalment,
    // paid to the disc as it detonates rather than lost. Not accrued for the detonation tick itself,
    // matching how DamageOverTime pays out on its expiry tick.
    var damage = charge.Damage + (charge.HasAura ? FP64.Floor(charge.AuraPending) : FP64.Zero);
    var radius = charge.Radius;
    var snareDurationTicks = charge.SnareDurationTicks;
    charge.Clear();

    var center = frame.Has<TransformComponent>(caster)
      ? frame.GetReadOnly<TransformComponent>(caster).Position
      : FPVector3.Zero;

    // Collected first, damaged after: ApplyDamage allocates the hit-id singleton on its first call of
    // the match, and that creates an entity while the filter is still walking storage.
    var hits = new List<EntityRef>();
    SkillAreas.Collect(ref frame, caster, center, radius, hits);

    RaiseDetonatedEvent(ref frame, caster, sourceId, center, radius, hits.Count);

    foreach (var target in hits) {
      if (damage > FP64.Zero)
        DamageApplication.ApplyDamage(ref frame, caster, target, damage, DamageType.Magical);
      Snares.Apply(ref frame, target, sourceId, snareDurationTicks);
    }
  }

  // Deals one accrued aura instalment to every hostile in the disc centred on the caster now.
  private static void PayAuraPulse(ref Frame frame, EntityRef caster, FP64 radius, FP64 damage) {
    var center = frame.Has<TransformComponent>(caster)
      ? frame.GetReadOnly<TransformComponent>(caster).Position
      : FPVector3.Zero;

    var hits = new List<EntityRef>();
    SkillAreas.Collect(ref frame, caster, center, radius, hits);

    foreach (var target in hits)
      DamageApplication.ApplyDamage(ref frame, caster, target, damage, DamageType.Magical);
  }

  public static void Clear(ref Frame frame, EntityRef entity) {
    if (frame.Has<SkillChargeComponent>(entity))
      frame.Get<SkillChargeComponent>(entity).Clear();
  }

  public static bool IsCharging(ref Frame frame, EntityRef entity) {
    return frame.Has<SkillChargeComponent>(entity) &&
           frame.GetReadOnly<SkillChargeComponent>(entity).IsCharging;
  }

  // Raised before the hits land, so the burst FX and the damage numbers arrive in that order.
  private static void RaiseDetonatedEvent(ref Frame frame, EntityRef caster, int skillAssetId,
    FPVector3 center, FP64 radius, int hitCount) {
    if (frame.EventRaiser == null)
      return;

    var evt = EventPool.Get<SkillChargeDetonatedEvent>();
    evt.CasterUnitId = UnitLookup.GetUnitId(ref frame, caster);
    evt.SkillAssetId = skillAssetId;
    evt.Position = center;
    evt.Radius = radius;
    evt.HitCount = hitCount;
    frame.EventRaiser.RaiseEvent(evt);
  }
}
