using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Deterministic.Random;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

// Stats.CritChance rolled against a stream derived from (world seed, attacker unit id, tick). The
// roll carries no state between ticks, so a rollback replay of a tick re-rolls the same result and
// a mispredicted crit corrects itself instead of drifting.
public static class CriticalStrikes {
  // Damage after the roll, and whether it crit. Applied before mitigation, so a crit is worth the
  // same fraction against every armor value.
  public static FP64 Scale(ref Frame frame, EntityRef attacker, int attackerUnitId, FP64 damage,
    out bool isCrit) {
    isCrit = Rolls(ref frame, attacker, attackerUnitId);
    return isCrit ? damage * frame.GetReadOnly<StatsComponent>(attacker).CritDamage : damage;
  }

  // One draw per (attacker, tick): an attacker lands at most one auto-attack in a tick, so nothing
  // shares a draw with itself.
  public static bool Rolls(ref Frame frame, EntityRef attacker, int attackerUnitId) {
    if (!frame.Has<StatsComponent>(attacker))
      return false;

    var chance = frame.GetReadOnly<StatsComponent>(attacker).CritChance;
    if (chance <= FP64.Zero)
      return false;

    var index = (ulong)(uint)attackerUnitId << 32 | (uint)frame.Tick;
    var rng = DeterministicRandom.FromSeed(SimRandom.WorldSeed(ref frame), SimRandom.CriticalStrikeKey,
      index);
    return rng.NextFixed() < chance; // NextFixed is [0, 1), so a chance of 1 always crits
  }
}
