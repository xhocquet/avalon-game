using xpTURN.Klotho.Deterministic.Math;

namespace Meesles.Avalon.Sim.Assets;

// One stat a skill's buff block lands on, parsed from a BuffStats entry. The effect code still owns
// which unit it hits; this is only the stat, the mode, and the rank ramp.
public readonly struct BuffSpec(StatType stat, BuffMode mode, FP64 baseValue, FP64 perRank) {
  public readonly StatType Stat = stat;
  public readonly BuffMode Mode = mode;
  public readonly FP64 Base = baseValue;
  public readonly FP64 PerRank = perRank;

  public FP64 MagnitudeAtRank(int rank) {
    return rank <= 0 ? FP64.Zero : Base + PerRank * FP64.FromInt(rank - 1);
  }
}
