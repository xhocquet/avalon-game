using System.Collections.Generic;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

public static class ChargeController {
  public static bool Arm(ref Frame frame, EntityRef entity, int sourceId, int delayTicks, FP64 damage,
    FP64 radius, int snareDurationTicks, FP64 auraDamagePerSecond = default) {
    if (sourceId == 0 || delayTicks <= 0 || radius <= FP64.Zero)
      return false;

    if (!frame.Has<SkillCharge>(entity))
      frame.Add(entity, new SkillCharge());

    ref var charge = ref frame.Get<SkillCharge>(entity);
    charge.SourceId = sourceId;
    charge.DetonateTick = frame.Tick + delayTicks;
    charge.SnareDurationTicks = snareDurationTicks;
    charge.Damage = damage;
    charge.Radius = radius;
    charge.AuraPending = FP64.Zero;

    if (auraDamagePerSecond > FP64.Zero) {
      var interval = TickMath.MsToTicksCeil(ref frame, DamageOverTimeController.PayoutIntervalMs);
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

  public static void TickAura(ref Frame frame, EntityRef caster) {
    if (!frame.Has<SkillCharge>(caster))
      return;

    ref var charge = ref frame.Get<SkillCharge>(caster);
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
    // ApplyDamage can create the hit-id singleton.
    PayAuraPulse(ref frame, caster, radius, whole);
  }

  public static void Detonate(ref Frame frame, EntityRef caster) {
    if (!frame.Has<SkillCharge>(caster))
      return;

    ref var charge = ref frame.Get<SkillCharge>(caster);
    if (!charge.IsCharging)
      return;

    var sourceId = charge.SourceId;
    // Pay accrued aura damage on detonation.
    var damage = charge.Damage + (charge.HasAura ? FP64.Floor(charge.AuraPending) : FP64.Zero);
    var radius = charge.Radius;
    var snareDurationTicks = charge.SnareDurationTicks;
    charge.Clear();

    var center = frame.Has<TransformComponent>(caster)
      ? frame.GetReadOnly<TransformComponent>(caster).Position
      : FPVector3.Zero;

    // ApplyDamage can create an entity; finish filtering first.
    var hits = new List<EntityRef>();
    CombatRange.CollectHostilesInRadius(ref frame, caster, center, radius, hits);

    RaiseDetonatedEvent(ref frame, caster, sourceId, center, radius, hits.Count);

    foreach (var target in hits) {
      if (damage > FP64.Zero)
        DamageController.ApplyDamage(ref frame, caster, target, damage, DamageType.Magical);
      SnareController.Apply(ref frame, target, sourceId, snareDurationTicks);
    }
  }

  private static void PayAuraPulse(ref Frame frame, EntityRef caster, FP64 radius, FP64 damage) {
    var center = frame.Has<TransformComponent>(caster)
      ? frame.GetReadOnly<TransformComponent>(caster).Position
      : FPVector3.Zero;

    var hits = new List<EntityRef>();
    CombatRange.CollectHostilesInRadius(ref frame, caster, center, radius, hits);

    foreach (var target in hits)
      DamageController.ApplyDamage(ref frame, caster, target, damage, DamageType.Magical);
  }

  public static void Clear(ref Frame frame, EntityRef entity) {
    if (frame.Has<SkillCharge>(entity))
      frame.Get<SkillCharge>(entity).Clear();
  }

  public static bool IsCharging(ref Frame frame, EntityRef entity) {
    return frame.Has<SkillCharge>(entity) &&
           frame.GetReadOnly<SkillCharge>(entity).IsCharging;
  }

  // Raise before damage so FX precedes damage numbers.
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
