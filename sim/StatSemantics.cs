using xpTURN.Klotho.Deterministic.Math;

namespace Meesles.Avalon.Sim;

// Which way a stat has to move to help the unit carrying it. Not a bound and not a tuning value - it
// is what lets a cleanse tell a slow from a haste when both sit in the same StatBuffs buffer as a
// signed MoveSpeed entry. Only the stats where less is more are named; everything else is higher-better.
public static class StatSemantics {
  public static bool HigherIsBetter(StatType stat) {
    return stat switch {
      StatType.AttackWindup => false, // Seconds between a swing starting and landing
      StatType.GameplayRadius => false, // The unit's own hurtbox - a bigger one is easier to hit
      _ => true
    };
  }

  // True when a timed modifier of `applied` moved `stat` the wrong way for its owner. The test a
  // cleanse uses to strip debuffs and keep buffs out of one buffer.
  public static bool IsAdverse(StatType stat, FP64 applied) {
    if (applied == FP64.Zero)
      return false;

    return HigherIsBetter(stat) ? applied < FP64.Zero : applied > FP64.Zero;
  }
}
