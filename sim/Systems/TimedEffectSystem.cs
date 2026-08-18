using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon;

// Every per-tick countdown in one pass: attack cooldowns, skill cooldowns, stat buffs, armed attack
// procs. Starting any of them is command-driven and lives with the rule that owns it (DamageSystem,
// SkillActions, StatBuffApplication, AttackProcs); burning them down is this.
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
  }
}
