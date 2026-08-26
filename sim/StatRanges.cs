using xpTURN.Klotho.Deterministic.Math;

namespace Meesles.Avalon.Sim;

// The bounds every stat is held inside, plus the value a unit starts at before its asset row is
// applied. These are not tuning: they are what keeps the sim from dividing by zero, inverting a
// mitigation curve, or leaving a unit with no health pool, so they live in code beside CommandLimits
// rather than in the asset JSON where a bad edit would reach the simulation.
//
// Rows is indexed by StatType and must stay in the enum's order - StatsComponent indexes both with
// the same int, and the static constructor asserts the count.
public static class StatRanges {
  public const int Count = (int)StatType.StatCount;

  public readonly struct Row(FP64 min, FP64 max, FP64 initial) {
    public readonly FP64 Min = min;
    public readonly FP64 Max = max;
    public readonly FP64 Initial = initial; // What a fresh StatsComponent holds before From() runs
  }

  private static readonly Row[] Rows = [
    /* MaxHealth        */ new(Int(1), Int(20000), Int(100)),
    /* MaxMana          */ new(Int(0), Int(10000), Int(0)),
    /* HealthRegen      */ new(Int(-100), Int(500), Int(0)),
    /* ManaRegen        */ new(Int(-100), Int(500), Int(0)),
    /* Armor            */ new(Int(-100), Int(1000), Int(0)),
    /* MagicResist      */ new(Int(-100), Int(1000), Int(0)),
    /* AttackDamage     */ new(Int(0), Int(5000), Int(1)),
    /* BaseAttackSpeed  */ new(Ratio(1, 10), Int(5), Int(1)),
    /* BonusAttackSpeed */ new(Ratio(-9, 10), Int(5), Int(0)),
    /* CritChance       */ new(Int(0), Int(1), Int(0)),
    /* CritDamage       */ new(Int(1), Int(5), Ratio(7, 4)),
    /* MoveSpeed        */ new(Int(0), Int(20), Int(0)),
    /* AttackRange      */ new(Int(0), Int(100), Int(1)),
    /* AcquisitionRange */ new(Int(0), Int(200), Int(1)),
    /* GameplayRadius   */ new(Int(0), Int(20), Int(0)),
    /* AttackWindup     */ new(Int(0), Int(5), Int(0))
  ];

  static StatRanges() {
    if (Rows.Length != Count)
      throw new System.InvalidOperationException(
        $"StatRanges.Rows has {Rows.Length} rows for {Count} StatType values");
  }

  public static Row Of(StatType stat) => Rows[(int)stat];

  public static FP64 Clamp(StatType stat, FP64 value) {
    var row = Rows[(int)stat];
    return FP64.Clamp(value, row.Min, row.Max);
  }

  private static FP64 Int(int value) => FP64.FromInt(value);

  // Exact in fixed point for the denominators used above, and avoids a float literal in sim code.
  private static FP64 Ratio(int numerator, int denominator) =>
    FP64.FromInt(numerator) / FP64.FromInt(denominator);
}
