using xpTURN.Klotho.Deterministic.Math;

namespace Meesles.Avalon.Sim.Assets;

public enum BuffMode {
  Percent = 0, // Adds a fraction of the stat's current value
  Flat = 1 // Adds the number as-is; for stats like BonusAttackSpeed where a fraction of ~0 is nothing
}

// One stat a skill's buff block lands on, parsed from a BuffStats entry. The effect code still owns
// which unit it hits; this is only the stat, the mode, and the rank ramp.
public readonly struct BuffSpec {
  public readonly StatType Stat;
  public readonly BuffMode Mode;
  public readonly FP64 Base;
  public readonly FP64 PerRank;

  public BuffSpec(StatType stat, BuffMode mode, FP64 baseValue, FP64 perRank) {
    Stat = stat;
    Mode = mode;
    Base = baseValue;
    PerRank = perRank;
  }

  public FP64 MagnitudeAtRank(int rank) {
    return rank <= 0 ? FP64.Zero : Base + PerRank * FP64.FromInt(rank - 1);
  }
}
