using xpTURN.Klotho.Deterministic.Math;

namespace Meesles.Avalon.Sim;

public enum BuffChangeKind {
  None = 0,
  StatFlat = 1,
  StatPercent = 2
}

public readonly record struct BuffChange(BuffChangeKind Kind, int Parameter, FP64 Magnitude) {
  public static BuffChange Stat(StatType stat, BuffMode mode, FP64 magnitude) =>
    new(mode == BuffMode.Flat ? BuffChangeKind.StatFlat : BuffChangeKind.StatPercent, (int)stat, magnitude);

  public bool IsStat => Kind is BuffChangeKind.StatFlat or BuffChangeKind.StatPercent;

  public EffectPolarity GetPolarity(FP64 baseValue) {
    var amount = Kind == BuffChangeKind.StatPercent ? baseValue * Magnitude : Magnitude;
    if (!IsStat || amount == FP64.Zero)
      return EffectPolarity.Neutral;

    var higherIsBetter = (StatType)Parameter is not (StatType.AttackWindup or StatType.GameplayRadius);
    return (amount > FP64.Zero) == higherIsBetter ? EffectPolarity.Buff : EffectPolarity.Debuff;
  }
}
