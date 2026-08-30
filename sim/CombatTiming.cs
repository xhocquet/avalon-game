using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

// Turns Stats.AttacksPerSecond into the tick period Combat counts down. Derived at the moment of the
// hit rather than stored, so bonuses stay additive on the rate, rounding never compounds, and the
// authored number keeps its meaning when the tick rate changes. The view shares it so a cooldown
// ring can't disagree with the cooldown the sim is running.
public static class CombatTiming {
  public static int CooldownTicks(ref Frame frame, EntityRef attacker) {
    if (!frame.Has<Stats>(attacker))
      return 1;

    var attacksPerSecond = frame.GetReadOnly<Stats>(attacker).AttacksPerSecond;
    var ticksPerSecond = FP64.FromInt(1000) / FP64.FromInt(TickMath.DeltaTimeMs(ref frame));

    var half = FP64.One / FP64.FromInt(2);
    var ticks = (ticksPerSecond / attacksPerSecond + half).ToInt(); // Round to nearest, not truncate
    return ticks < 1 ? 1 : ticks;
  }

  // Windup is authored in seconds and does not scale with attack speed, so a unit that attacks
  // faster spends proportionally more of its period standing in the recovery half of the swing.
  // Floors at 0, not 1: a unit with no authored windup lands its damage on the tick it swings.
  //
  // Held under the cooldown this swing is actually paying, because Combat tracks one swing at a time
  // and the next one cannot start until this one resolves. Passing the burst spacing rather than the
  // unit's own period is what keeps a burst faster than the windup landing at the spacing it was
  // authored with instead of being throttled down to the wind-up.
  public static int WindupTicks(ref Frame frame, EntityRef attacker, int cooldownTicks) {
    if (!frame.Has<Stats>(attacker))
      return 0;

    var windup = frame.GetReadOnly<Stats>(attacker).AttackWindup;
    var ticks = SecondsToTicks(ref frame, windup, minimum: 0);
    return ticks >= cooldownTicks ? cooldownTicks - 1 : ticks;
  }

  private static int SecondsToTicks(ref Frame frame, FP64 seconds, int minimum) {
    var ticksPerSecond = FP64.FromInt(1000) / FP64.FromInt(TickMath.DeltaTimeMs(ref frame));
    var half = FP64.One / FP64.FromInt(2);
    var ticks = (seconds * ticksPerSecond + half).ToInt(); // Round to nearest, not truncate
    return ticks < minimum ? minimum : ticks;
  }
}
