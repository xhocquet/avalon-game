using System.Collections.Generic;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

// Advances cooldowns and timed effects before systems that consume them.
public class TimedEffectSystem : ISystem {
  private readonly List<EntityRef> _detonating = [];
  private readonly List<EntityRef> _pulsing = [];
  private readonly List<EntityRef> _completingChannels = [];
  private readonly List<EntityRef> _cancellingChannels = [];
  private readonly List<EntityRef> _burning = [];

  public void Update(ref Frame frame) {
    var attackers = frame.Filter<Combat>();
    while (attackers.Next(out var entity)) {
      ref var combat = ref frame.Get<Combat>(entity);
      if (combat.CooldownRemainingTicks > 0)
        combat.CooldownRemainingTicks--;
    }

    // Commands run before Update, so casts lose a cooldown tick immediately.
    var casters = frame.Filter<Skills>();
    while (casters.Next(out var entity))
      frame.Get<Skills>(entity).TickCooldowns();

    var buffed = frame.Filter<StatBuffs, Stats>();
    while (buffed.Next(out var entity))
      BuffsController.ExpireDue(ref frame, entity);

    // Clear in place to preserve filter iteration.
    var armed = frame.Filter<AttackProc>();
    while (armed.Next(out var entity)) {
      ref var proc = ref frame.Get<AttackProc>(entity);
      if (proc.IsExpired(frame.Tick))
        proc.Clear();
    }

    var bursting = frame.Filter<AttackBurst>();
    while (bursting.Next(out var entity)) {
      ref var burst = ref frame.Get<AttackBurst>(entity);
      if (burst.IsExpired(frame.Tick))
        burst.Clear();
    }

    var snared = frame.Filter<Snare>();
    while (snared.Next(out var entity)) {
      ref var snare = ref frame.Get<Snare>(entity);
      if (snare.IsExpired(frame.Tick))
        snare.Clear();
    }

    var silenced = frame.Filter<Silence>();
    while (silenced.Next(out var entity)) {
      ref var silence = ref frame.Get<Silence>(entity);
      if (silence.IsExpired(frame.Tick))
        silence.Clear();
    }

    // Damage may create the hit-id singleton, so tick after filtering.
    _burning.Clear();
    var burning = frame.Filter<DamageOverTime>();
    while (burning.Next(out var entity))
      if (frame.GetReadOnly<DamageOverTime>(entity).IsBurning)
        _burning.Add(entity);

    for (var i = 0; i < _burning.Count; i++)
      DamageOverTimeController.Tick(ref frame, _burning[i]);

    // Charge effects can mutate entities while applying damage, so defer them.
    _detonating.Clear();
    _pulsing.Clear();
    var charging = frame.Filter<SkillCharge>();
    while (charging.Next(out var entity)) {
      ref readonly var charge = ref frame.GetReadOnly<SkillCharge>(entity);
      if (charge.IsDue(frame.Tick))
        _detonating.Add(entity);
      else if (charge.HasAura)
        _pulsing.Add(entity);
    }

    for (var i = 0; i < _pulsing.Count; i++)
      ChargeController.TickAura(ref frame, _pulsing[i]);

    for (var i = 0; i < _detonating.Count; i++)
      ChargeController.Detonate(ref frame, _detonating[i]);

    _completingChannels.Clear();
    _cancellingChannels.Clear();
    var channels = frame.Filter<SkillChannel>();
    while (channels.Next(out var entity)) {
      ref readonly var channel = ref frame.GetReadOnly<SkillChannel>(entity);
      if (!channel.IsActive)
        continue;
      if (ChannelController.HasMoved(ref frame, entity))
        _cancellingChannels.Add(entity);
      else if (channel.IsDue(frame.Tick))
        _completingChannels.Add(entity);
    }

    for (var i = 0; i < _cancellingChannels.Count; i++)
      ChannelController.Clear(ref frame, _cancellingChannels[i]);

    for (var i = 0; i < _completingChannels.Count; i++)
      ChannelController.Complete(ref frame, _completingChannels[i]);
  }
}
