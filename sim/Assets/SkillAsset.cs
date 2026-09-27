using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Assets;

// Instance ids live in the AssetIds.Skill* block; look one up with Get<SkillAsset>(id)
[KlothoDataAsset(AssetIds.TypeIds.Skill)]
public partial class SkillAsset : IDataAsset {
  [KlothoOrder(29)] public string Description;
  [KlothoOrder(0)] public int MaxRank = 4;
  [KlothoOrder(1)] public int CooldownMs;

  [KlothoOrder(21)] public bool SelfCast; // Self-cast affects button press timing

  [KlothoOrder(57)] public FP64 ManaCost; // Mana cost
  [KlothoOrder(58)] public FP64 ManaCostPerRank; // Increase in mana cost per rank

  [KlothoOrder(49)] public bool ClearsDebuffs; // 'Cleanse'
  [KlothoOrder(43)] public int SilenceDurationMs;

  // ==========================================================================
  // Cast range
  // ==========================================================================
  [KlothoOrder(30)] public FP64 AreaRadius; // circle cast shape
  [KlothoOrder(10)] public FP64 MinCastRange;
  [KlothoOrder(11)] public FP64 MaxCastRange;
  [KlothoOrder(27)] public FP64 ConeRange; // cone cast shape
  [KlothoOrder(28)] public FP64 ConeAngleDegrees;

  // ==========================================================================
  // Damage
  // ==========================================================================
  [KlothoOrder(2)] public FP64 Damage;
  [KlothoOrder(3)] public FP64 DamagePerRank;

  // ==========================================================================
  // Projectile
  // ==========================================================================
  [KlothoOrder(7)] public int ProjectileCount = 0;
  [KlothoOrder(4)] public FP64 ProjectileSpeed; // Units/second
  [KlothoOrder(5)] public FP64 ProjectileRange; // Units. Starts from offset
  [KlothoOrder(9)] public FP64 ProjectileSpawnOffset; // Units from caster
  [KlothoOrder(6)] public FP64 ProjectileRadius;
  [KlothoOrder(8)] public FP64 ProjectileSpacing;

  // ==========================================================================
  // Buff
  // ==========================================================================
  [KlothoOrder(12)] public int BuffDurationMs;
  [KlothoOrder(41)] public int BuffDurationMsPerRank; // Length increase per rank
  [KlothoOrder(13)] public FP64 BuffPercent;
  [KlothoOrder(14)] public FP64 BuffPercentPerRank; // Buff % increase per rank

  [KlothoOrder(38)] public string BuffStats;
  // BuffSpecs implements:
  //   "Armor,MagicResist" - bare name(s), uses BuffPercent
  //   "MoveSpeed pct 0.15 0.05"- <StatName> <pct|flat> <base> [perRank]
  //   "BonusAttackSpeed flat 0.10 0.10; Armor pct -0.20 -0.05"

  // ==========================================================================
  // Charged/procced attack
  // ==========================================================================
  [KlothoOrder(15)] public int ProcDurationMs; // How long the charge stays available
  [KlothoOrder(16)] public FP64 ProcDamageMultiplier; // 4.0 = 400%
  [KlothoOrder(17)] public FP64 ProcDamageMultiplierPerRank; // Increase in % per rank
  [KlothoOrder(18)] public bool ProcResetsAttackCooldown; // Whether auto-attack CD is cleared

  // ==========================================================================
  // Heal . Percent and flat amounts/increases cannot be mixed
  // ==========================================================================
  [KlothoOrder(19)] public FP64 HealPercent; // % of total health to heal
  [KlothoOrder(20)] public FP64 HealPercentPerRank; // Increase in % per rank
  [KlothoOrder(39)] public FP64 HealAmount; // Flat heal amount
  [KlothoOrder(40)] public FP64 HealAmountPerRank; // Flat increase per rank
  [KlothoOrder(55)] public FP64 ManaRestore;
  [KlothoOrder(56)] public FP64 ManaRestorePerRank;

  // ==========================================================================
  // Burstable auto-attack
  // ==========================================================================
  [KlothoOrder(22)] public int BurstAttackCount; // # of auto-attacks
  [KlothoOrder(23)] public int BurstAttackCountPerRank; // increase in auto-attacks per rank
  [KlothoOrder(24)] public int BurstAttackDelayMs; // Ms between bursted auto-attacks
  [KlothoOrder(25)] public int BurstDurationMs; // How long this burst stays available
  [KlothoOrder(26)] public bool BurstResetsAttackCooldown; // Whether this skill reset auto-attacks

  // ==========================================================================
  // Snare
  // ==========================================================================
  [KlothoOrder(31)] public int SnareDurationMs;
  [KlothoOrder(32)] public int SnareDurationMsPerRank;

  // ==========================================================================
  // Charge/Wind-up
  // ==========================================================================
  [KlothoOrder(33)] public int ChargeDurationMs;
  [KlothoOrder(47)] public int ChargeDurationMsPerRank; // Increase in charge time per rank
  [KlothoOrder(34)] public bool ChargeRootsCaster; // Can caster move during charge
  [KlothoOrder(48)] public bool ChargeCancelsOnMove; // Caster move cancels charge

  // ==========================================================================
  // Damage-over-time (DOT)
  // ==========================================================================
  [KlothoOrder(35)] public FP64 DotDamagePerSecond;
  [KlothoOrder(36)] public FP64 DotDamagePerSecondPerRank; // Increase in dmg/s per rank
  [KlothoOrder(37)] public int DotDurationMs;

  // ==========================================================================
  // Trail left behind the caster
  // ==========================================================================
  [KlothoOrder(44)] public int TrailDurationMs; // How long the trail emission takes
  [KlothoOrder(45)] public int TrailDurationMsPerRank;
  [KlothoOrder(46)] public FP64 TrailWidth; // Diameter of the trail segment
  [KlothoOrder(59)] public int TrailSegmentCount; // Total # of segments
  [KlothoOrder(60)] public int TrailSegmentIntervalMs; // Ms between each segment

  // ==========================================================================
  // Stockpile . These skills accumulate to StockpileMax over time
  // Regular cooldown Ms affects how quickly you can cast stockpiled skills
  // ==========================================================================
  [KlothoOrder(52)] public int StockpileMax;
  [KlothoOrder(53)] public int StockpileIntervalMs; // Time it takes to generate 1
  [KlothoOrder(54)] public int StockpileIntervalMsPerRank;

  // ==========================================================================
  // Dash
  // ==========================================================================
  [KlothoOrder(42)] public FP64 DashDistance; // in units
  [KlothoOrder(50)] public int DashCount; // # of dashes allowed per cast
  [KlothoOrder(51)] public int DashCountPerRank; // increase in dashes per cast per rank
  [KlothoOrder(61)] public FP64 DashSpeed; // Units/second
  [KlothoOrder(62)] public FP64 DashWidth; // Width in units

  private BuffSpec[] _buffSpecs;

  public bool HasCastRange => MinCastRange > FP64.Zero || MaxCastRange > FP64.Zero;
  public bool HasCone => ConeRange > FP64.Zero && ConeAngleDegrees > FP64.Zero;
  public bool HasArea => AreaRadius > FP64.Zero;
  public bool HasDash => DashDistance > FP64.Zero;
  public bool HasStockpile => StockpileMax > 0;
  public bool HasSilence => SilenceDurationMs > 0;
  public bool HasTrail => TrailDurationMs > 0 && TrailSegmentCount > 0;

  public FP64 DamageAtRank(int rank) {
    return rank <= 0 ? FP64.Zero : Damage + DamagePerRank * FP64.FromInt(rank - 1);
  }

  // Parsed BuffStats, empty when the row names none. Cached: the row is immutable once loaded, and the
  // parse is a pure function of the authored string, so both peers land on the same array. Integer-only
  // number parse, so the fixed-point values are bit-identical across peers.
  public BuffSpec[] BuffSpecs => _buffSpecs ??= ParseBuffSpecs();

  private BuffSpec[] ParseBuffSpecs() {
    if (string.IsNullOrWhiteSpace(BuffStats))
      return System.Array.Empty<BuffSpec>();

    var specs = new System.Collections.Generic.List<BuffSpec>();
    foreach (var raw in BuffStats.Split(';')) {
      var entry = raw.Trim();
      if (entry.Length == 0)
        continue;

      var tokens = entry.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries);
      if (tokens.Length == 1) {
        // bare name, or a comma list of them, on the scalar BuffPercent pair
        foreach (var name in tokens[0].Split(','))
          specs.Add(new BuffSpec(System.Enum.Parse<StatType>(name.Trim()), BuffMode.Percent,
            BuffPercent, BuffPercentPerRank));
        continue;
      }

      var mode = tokens[1].ToLowerInvariant() switch {
        "flat" => BuffMode.Flat,
        "pct" => BuffMode.Percent,
        _ => throw new System.FormatException($"BuffStats '{entry}': mode must be 'pct' or 'flat'")
      };
      var perRank = tokens.Length > 3 ? ParseFixed(tokens[3]) : FP64.Zero;
      specs.Add(new BuffSpec(System.Enum.Parse<StatType>(tokens[0]), mode, ParseFixed(tokens[2]), perRank));
    }

    return specs.ToArray();
  }

  // Decimal string -> FP64 without touching float: "-0.035" becomes -(0 + 35/1000). Authored data, so
  // a malformed number throws rather than clamping.
  private static FP64 ParseFixed(string token) {
    var s = token.Trim();
    var negative = s.StartsWith("-");
    if (negative || s.StartsWith("+"))
      s = s.Substring(1);

    var dot = s.IndexOf('.');
    FP64 value;
    if (dot < 0) {
      value = FP64.FromInt(int.Parse(s));
    }
    else {
      var frac = s.Substring(dot + 1);
      var denom = 1;
      for (var i = 0; i < frac.Length; i++)
        denom *= 10;
      var whole = dot == 0 ? 0 : int.Parse(s.Substring(0, dot));
      var numer = frac.Length == 0 ? 0 : int.Parse(frac);
      value = FP64.FromInt(whole) + FP64.FromInt(numer) / FP64.FromInt(denom);
    }

    return negative ? -value : value;
  }

  public FP64 DotDamagePerSecondAtRank(int rank) {
    return rank <= 0
      ? FP64.Zero
      : DotDamagePerSecond + DotDamagePerSecondPerRank * FP64.FromInt(rank - 1);
  }

  public FP64 BuffPercentAtRank(int rank) {
    return rank <= 0 ? FP64.Zero : BuffPercent + BuffPercentPerRank * FP64.FromInt(rank - 1);
  }

  public FP64 HealPercentAtRank(int rank) {
    return rank <= 0 ? FP64.Zero : HealPercent + HealPercentPerRank * FP64.FromInt(rank - 1);
  }

  public FP64 HealAmountAtRank(int rank) {
    return rank <= 0 ? FP64.Zero : HealAmount + HealAmountPerRank * FP64.FromInt(rank - 1);
  }

  public int BuffDurationMsAtRank(int rank) {
    return rank <= 0 ? 0 : BuffDurationMs + BuffDurationMsPerRank * (rank - 1);
  }

  public int TrailDurationMsAtRank(int rank) {
    return rank <= 0 ? 0 : TrailDurationMs + TrailDurationMsPerRank * (rank - 1);
  }

  public int DashCountAtRank(int rank) {
    return rank <= 0 ? 0 : DashCount + DashCountPerRank * (rank - 1);
  }

  public FP64 ManaRestoreAtRank(int rank) {
    return rank <= 0 ? FP64.Zero : ManaRestore + ManaRestorePerRank * FP64.FromInt(rank - 1);
  }

  public FP64 ManaCostAtRank(int rank) {
    return rank <= 0 ? FP64.Zero : ManaCost + ManaCostPerRank * FP64.FromInt(rank - 1);
  }

  // Floored at one tick's worth so a deep rank can't drive the accrual interval to zero or negative.
  public int StockpileIntervalMsAtRank(int rank) {
    if (rank <= 0)
      return 0;
    var ms = StockpileIntervalMs + StockpileIntervalMsPerRank * (rank - 1);
    return ms < 1 ? 1 : ms;
  }

  // Floored at 0: ChargeDurationMsPerRank is negative for a row that charges faster each rank.
  public int ChargeDurationMsAtRank(int rank) {
    if (rank <= 0)
      return 0;
    var ms = ChargeDurationMs + ChargeDurationMsPerRank * (rank - 1);
    return ms < 0 ? 0 : ms;
  }

  public int BurstAttackCountAtRank(int rank) {
    return rank <= 0 ? 0 : BurstAttackCount + BurstAttackCountPerRank * (rank - 1);
  }

  public int SnareDurationMsAtRank(int rank) {
    return rank <= 0 ? 0 : SnareDurationMs + SnareDurationMsPerRank * (rank - 1);
  }

  // Flat, rank-gated so an unlearned slot silences for nothing. No per-rank ramp today.
  public int SilenceDurationMsAtRank(int rank) {
    return rank <= 0 ? 0 : SilenceDurationMs;
  }

  public FP64 ProcDamageMultiplierAtRank(int rank) {
    return rank <= 0
      ? FP64.Zero
      : ProcDamageMultiplier + ProcDamageMultiplierPerRank * FP64.FromInt(rank - 1);
  }
}
