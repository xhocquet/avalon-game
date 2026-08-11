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
    if (!frame.Has<StatsComponent>(attacker))
      return 1;

    var attacksPerSecond = frame.GetReadOnly<StatsComponent>(attacker).AttacksPerSecond;
    var ticksPerSecond = FP64.FromInt(1000) / FP64.FromInt(TickMath.DeltaTimeMs(ref frame));

    var half = FP64.One / FP64.FromInt(2);
    var ticks = (ticksPerSecond / attacksPerSecond + half).ToInt(); // Round to nearest, not truncate
    return ticks < 1 ? 1 : ticks;
  }
}
