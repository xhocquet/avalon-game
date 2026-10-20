using xpTURN.Klotho.Deterministic.Math;

namespace Meesles.Avalon.Sim;

public enum BuffChangeKind {
  None = 0,
  Stat = 1
}

public readonly record struct BuffChange(BuffChangeKind Kind, int Parameter, FP64 Applied) {
  public static BuffChange Stat(StatType stat, FP64 applied) => new(BuffChangeKind.Stat, (int)stat, applied);

  public EffectPolarity Polarity {
    get {
      if (Kind != BuffChangeKind.Stat || Applied == FP64.Zero)
        return EffectPolarity.Neutral;

      var higherIsBetter = (StatType)Parameter is not (StatType.AttackWindup or StatType.GameplayRadius);
      return (Applied > FP64.Zero) == higherIsBetter ? EffectPolarity.Buff : EffectPolarity.Debuff;
    }
  }
}
