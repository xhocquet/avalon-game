using System.Collections.Generic;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Components;
using Meesles.Avalon.Sim.Heroes;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon;

// Every per-tick countdown in one pass: attack cooldowns, skill cooldowns, stat buffs, armed attack
// procs, queued attack bursts, snares, damage-over-time burns, charging skill bursts. Starting any of
// them is command-driven and lives with the rule that owns it (DamageSystem, SkillActions,
// StatBuffApplication, AttackProcs, AttackBursts, Snares, DamageOverTime, SkillCharges); burning them
// down is this.
//
// Registered ahead of everything that reads Stats, casts, or deals damage for the frame, so an effect
// that ended never pays out one more tick and a cooldown that reached 0 is spendable on the same tick
// it does. Nothing between here and those readers touches a countdown, so this is the same frame the
// separate cooldown systems produced.
//
// The two kinds of expiry differ: a cooldown counts down toward 0 because a paused or refunded one is
// a real mechanic, while buffs and procs hold an absolute expiry tick because they are set once and
// only ever compared against.
public class TimedEffectSystem : ISystem {
  private readonly List<EntityRef> _detonating = [];
  private readonly List<EntityRef> _pulsing = [];
  private readonly List<EntityRef> _burning = [];

  public void Update(ref Frame frame) {
    var attackers = frame.Filter<Combat>();
    while (attackers.Next(out var entity)) {
      ref var combat = ref frame.Get<Combat>(entity);
      if (combat.CooldownRemainingTicks > 0)
        combat.CooldownRemainingTicks--;
    }

    // A cast on tick N loses one tick here on that same tick, because commands are delivered before
    // the Update phase. Identical on both peers, so it is not an off-by-one.
    var casters = frame.Filter<SkillsComponent>();
    while (casters.Next(out var entity))
      frame.Get<SkillsComponent>(entity).TickCooldowns();

    var buffed = frame.Filter<StatBuffsComponent, StatsComponent>();
    while (buffed.Next(out var entity))
      StatBuffApplication.ExpireDue(ref frame, entity);

    // Clears the slot in place rather than removing the component, so nothing here touches the
    // filter's own component types mid-walk.
    var armed = frame.Filter<AttackProcComponent>();
    while (armed.Next(out var entity)) {
      ref var proc = ref frame.Get<AttackProcComponent>(entity);
      if (proc.IsExpired(frame.Tick))
        proc.Clear();
    }

    var bursting = frame.Filter<AttackBurstComponent>();
    while (bursting.Next(out var entity)) {
      ref var burst = ref frame.Get<AttackBurstComponent>(entity);
      if (burst.IsExpired(frame.Tick))
        burst.Clear();
    }

    var snared = frame.Filter<SnareComponent>();
    while (snared.Next(out var entity)) {
      ref var snare = ref frame.Get<SnareComponent>(entity);
      if (snare.IsExpired(frame.Tick))
        snare.Clear();
    }

    // Deferred like the detonation below: DamageApplication allocates the hit-id singleton on its
    // first call of the match, and that creates an entity while a filter is still walking storage.
    _burning.Clear();
    var burning = frame.Filter<DamageOverTimeComponent>();
    while (burning.Next(out var entity))
      if (frame.GetReadOnly<DamageOverTimeComponent>(entity).IsBurning)
        _burning.Add(entity);

    for (var i = 0; i < _burning.Count; i++)
      DamageOverTime.Tick(ref frame, _burning[i]);

    // The one countdown here that pays something out rather than just ending. Deferred because the
    // detonation - and a channel aura's per-interval pulse - walks the units itself and hits what it
    // catches, which is this filter's own type. Damage landing this early in the frame still reaches
    // DeathSystem on the same tick. A charge whose wind-up is done detonates; one still winding up
    // with an aura pulses.
    _detonating.Clear();
    _pulsing.Clear();
    var charging = frame.Filter<SkillChargeComponent>();
    while (charging.Next(out var entity)) {
      ref readonly var charge = ref frame.GetReadOnly<SkillChargeComponent>(entity);
      if (charge.IsDue(frame.Tick))
        _detonating.Add(entity);
      else if (charge.HasAura)
        _pulsing.Add(entity);
    }

    for (var i = 0; i < _pulsing.Count; i++)
      SkillCharges.TickAura(ref frame, _pulsing[i]);

    for (var i = 0; i < _detonating.Count; i++)
      SkillCharges.Detonate(ref frame, _detonating[i]);
  }
}
