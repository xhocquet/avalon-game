using System.Collections.Generic;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Components;
using Meesles.Avalon.Sim.Heroes;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon;

// Every per-tick countdown in one pass: attack cooldowns, skill cooldowns, stat buffs, armed attack
// procs, queued attack bursts, snares, charging skill bursts. Starting any of them is command-driven
// and lives with the rule that owns it (DamageSystem, SkillActions, StatBuffApplication, AttackProcs,
// AttackBursts, Snares, SkillCharges); burning them down is this.
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

    // The one countdown here that pays something out rather than just ending. Deferred because the
    // detonation walks the units itself and snares what it catches, which is this filter's own type.
    // Damage landing this early in the frame still reaches DeathSystem on the same tick.
    _detonating.Clear();
    var charging = frame.Filter<SkillChargeComponent>();
    while (charging.Next(out var entity))
      if (frame.GetReadOnly<SkillChargeComponent>(entity).IsDue(frame.Tick))
        _detonating.Add(entity);

    for (var i = 0; i < _detonating.Count; i++)
      SkillCharges.Detonate(ref frame, _detonating[i]);
  }
}
