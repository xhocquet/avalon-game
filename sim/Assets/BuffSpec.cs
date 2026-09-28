using xpTURN.Klotho.Deterministic.Math;

namespace Meesles.Avalon.Sim.Assets;

// One parsed BuffStats entry.
public readonly struct BuffSpec(StatType stat, BuffMode mode, FP64 baseValue, FP64 perRank) {
  public readonly StatType Stat = stat;
  public readonly BuffMode Mode = mode;
  public readonly FP64 Base = baseValue;
  public readonly FP64 PerRank = perRank;

  public FP64 MagnitudeAtRank(int rank) {
    return rank <= 0 ? FP64.Zero : Base + PerRank * FP64.FromInt(rank - 1);
  }
}
