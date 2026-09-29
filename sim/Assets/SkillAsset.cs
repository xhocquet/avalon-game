using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Assets;

// Instance ids live in the AssetIds.Skill* block; look one up with Get<SkillAsset>(id)
[KlothoDataAsset(AssetIds.TypeIds.Skill)]
public partial class SkillAsset : IDataAsset {
  [KlothoOrder(0)] public string Description;
  [KlothoOrder(1)] public int MaxRank = 4;
  [KlothoOrder(2)] public int CooldownMs;

  [KlothoOrder(3)] public bool SelfCast; // Self-cast affects button press timing

  [KlothoOrder(4)] public FP64 ManaCost; // Mana cost
  [KlothoOrder(5)] public FP64 ManaCostPerRank; // Increase in mana cost per rank

  [KlothoOrder(6)] public bool ClearsDebuffs; // 'Cleanse'
  [KlothoOrder(7)] public int SilenceDurationMs;
  [KlothoOrder(8)] public int SilenceDurationMsPerRank;

  // ==========================================================================
  // Cast range
  // ==========================================================================
  [KlothoOrder(9)] public FP64 AreaRadius; // circle cast shape
  [KlothoOrder(10)] public FP64 MinCastRange;
  [KlothoOrder(11)] public FP64 MaxCastRange;
  [KlothoOrder(12)] public FP64 ConeRange; // cone cast shape
  [KlothoOrder(13)] public FP64 ConeAngleDegrees;

  // ==========================================================================
  // Damage
  // ==========================================================================
  [KlothoOrder(14)] public FP64 Damage;
  [KlothoOrder(15)] public FP64 DamagePerRank;

  // ==========================================================================
  // Projectile
  // ==========================================================================
  [KlothoOrder(16)] public int ProjectileCount = 0;
  [KlothoOrder(17)] public FP64 ProjectileSpeed; // Units/second
  [KlothoOrder(18)] public FP64 ProjectileRange; // Units. Starts from offset
  [KlothoOrder(19)] public FP64 ProjectileSpawnOffset; // Units from caster
  [KlothoOrder(20)] public FP64 ProjectileRadius;
  [KlothoOrder(21)] public FP64 ProjectileSpacing;

  // ==========================================================================
  // Buff
  // ==========================================================================
  [KlothoOrder(22)] public int BuffDurationMs;
  [KlothoOrder(23)] public int BuffDurationMsPerRank; // Length increase per rank
  [KlothoOrder(24)] public FP64 BuffPercent;
  [KlothoOrder(25)] public FP64 BuffPercentPerRank; // Buff % increase per rank

  [KlothoOrder(26)] public string BuffStats;
  // BuffSpecs implements:
  //   "Armor,MagicResist" - bare name(s), uses BuffPercent
  //   "MoveSpeed pct 0.15 0.05"- <StatName> <pct|flat> <base> [perRank]
  //   "BonusAttackSpeed flat 0.10 0.10; Armor pct -0.20 -0.05"

  // ==========================================================================
  // Charged/procced attack
  // ==========================================================================
  [KlothoOrder(27)] public int ProcDurationMs; // How long the charge stays available
  [KlothoOrder(28)] public FP64 ProcDamageMultiplier; // 4.0 = 400%
  [KlothoOrder(29)] public FP64 ProcDamageMultiplierPerRank; // Increase in % per rank
  [KlothoOrder(30)] public bool ProcResetsAttackCooldown; // Whether auto-attack CD is cleared

  // ==========================================================================
  // Heal . Percent and flat amounts/increases cannot be mixed
  // ==========================================================================
  [KlothoOrder(31)] public FP64 HealPercent; // % of total health to heal
  [KlothoOrder(32)] public FP64 HealPercentPerRank; // Increase in % per rank
  [KlothoOrder(33)] public FP64 HealAmount; // Flat heal amount
  [KlothoOrder(34)] public FP64 HealAmountPerRank; // Flat increase per rank
  [KlothoOrder(35)] public FP64 ManaRestore;
  [KlothoOrder(36)] public FP64 ManaRestorePerRank;

  // ==========================================================================
  // Burstable auto-attack
  // ==========================================================================
  [KlothoOrder(37)] public int BurstAttackCount; // # of auto-attacks
  [KlothoOrder(38)] public int BurstAttackCountPerRank; // increase in auto-attacks per rank
  [KlothoOrder(39)] public int BurstAttackDelayMs; // Ms between bursted auto-attacks
  [KlothoOrder(40)] public int BurstDurationMs; // How long this burst stays available
  [KlothoOrder(41)] public bool BurstResetsAttackCooldown; // Whether this skill reset auto-attacks

  // ==========================================================================
  // Snare
  // ==========================================================================
  [KlothoOrder(42)] public int SnareDurationMs;
  [KlothoOrder(43)] public int SnareDurationMsPerRank;

  // ==========================================================================
  // Charge/Wind-up
  // ==========================================================================
  [KlothoOrder(44)] public int ChargeDurationMs;
  [KlothoOrder(45)] public int ChargeDurationMsPerRank; // Increase in charge time per rank
  [KlothoOrder(46)] public bool ChargeRootsCaster; // Can caster move during charge
  [KlothoOrder(47)] public bool ChargeCancelsOnMove; // Caster move cancels charge

  // ==========================================================================
  // Damage-over-time (DOT)
  // ==========================================================================
  [KlothoOrder(48)] public FP64 DotDamagePerSecond;
  [KlothoOrder(49)] public FP64 DotDamagePerSecondPerRank; // Increase in dmg/s per rank
  [KlothoOrder(50)] public int DotDurationMs;

  // ==========================================================================
  // Trail left behind the caster
  // ==========================================================================
  [KlothoOrder(51)] public int TrailDurationMs; // How long the trail emission takes
  [KlothoOrder(52)] public int TrailDurationMsPerRank;
  [KlothoOrder(53)] public FP64 TrailWidth; // Diameter of the trail segment
  [KlothoOrder(54)] public int TrailSegmentCount; // Total # of segments
  [KlothoOrder(55)] public int TrailSegmentIntervalMs; // Ms between each segment

  // ==========================================================================
  // Stockpile . These skills accumulate to StockpileMax over time
  // Regular cooldown Ms affects how quickly you can cast stockpiled skills
  // ==========================================================================
  [KlothoOrder(56)] public int StockpileMax;
  [KlothoOrder(57)] public int StockpileIntervalMs; // Time it takes to generate 1
  [KlothoOrder(58)] public int StockpileIntervalMsPerRank;

  // ==========================================================================
  // Dash
  // ==========================================================================
  [KlothoOrder(59)] public FP64 DashDistance; // in units
  [KlothoOrder(60)] public int DashCount; // # of dashes allowed per cast
  [KlothoOrder(61)] public int DashCountPerRank; // increase in dashes per cast per rank
  [KlothoOrder(62)] public FP64 DashSpeed; // Units/second
  [KlothoOrder(63)] public FP64 DashWidth; // Width in units


  public bool HasCastRange => MinCastRange > FP64.Zero || MaxCastRange > FP64.Zero;
  public bool HasCone => ConeRange > FP64.Zero && ConeAngleDegrees > FP64.Zero;
  public bool HasArea => AreaRadius > FP64.Zero;
  public bool HasDash => DashDistance > FP64.Zero;
  public bool HasSilence => SilenceDurationMs > 0;
  public bool HasTrail => TrailDurationMs > 0 && TrailSegmentCount > 0;

  public int BuffDurationMsAtRank(int rank) => AtRank(BuffDurationMs, BuffDurationMsPerRank, rank);
  public int TrailDurationMsAtRank(int rank) => AtRank(TrailDurationMs, TrailDurationMsPerRank, rank);
  public int BurstAttackCountAtRank(int rank) => AtRank(BurstAttackCount, BurstAttackCountPerRank, rank);
  public int SnareDurationMsAtRank(int rank) => AtRank(SnareDurationMs, SnareDurationMsPerRank, rank);
  public int SilenceDurationMsAtRank(int rank) => AtRank(SilenceDurationMs, SilenceDurationMsPerRank, rank);
  public FP64 ProcDamageMultiplierAtRank(int rank) => AtRank(ProcDamageMultiplier, ProcDamageMultiplierPerRank, rank);
  public FP64 ManaCostAtRank(int rank) => AtRank(ManaCost, ManaCostPerRank, rank);
  public FP64 DotDamagePerSecondAtRank(int rank) => AtRank(DotDamagePerSecond, DotDamagePerSecondPerRank, rank);
  public FP64 DamageAtRank(int rank) => AtRank(Damage, DamagePerRank, rank);
  public int DashCountAtRank(int rank) => AtRank(DashCount, DashCountPerRank, rank);

  // Floored at 0: ChargeDurationMsPerRank is negative for a row that charges faster each rank.
  public int ChargeDurationMsAtRank(int rank) =>
    System.Math.Max(0, AtRank(ChargeDurationMs, ChargeDurationMsPerRank, rank));

  public static FP64 AtRank(FP64 baseValue, FP64 perRank, int rank) =>
    rank <= 0 ? FP64.Zero : baseValue + perRank * FP64.FromInt(rank - 1);

  public static int AtRank(int baseValue, int perRank, int rank) =>
    rank <= 0 ? 0 : baseValue + perRank * (rank - 1);

  // Parsed array of buffs from a skill's `BuffStats`
  public BuffSpec[] BuffSpecs => field ??= ParseBuffSpecs();

  private BuffSpec[] ParseBuffSpecs() {
    if (string.IsNullOrWhiteSpace(BuffStats))
      return [];

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
        _ => throw new System.FormatException()
      };
      var perRank = tokens.Length > 3 ? ParseFixed(tokens[3]) : FP64.Zero;
      specs.Add(new BuffSpec(System.Enum.Parse<StatType>(tokens[0]), mode, ParseFixed(tokens[2]), perRank));
    }

    return [.. specs];
  }

  // Parses decimal string ("-0.34") to FP64
  private static FP64 ParseFixed(string token) {
    FP64 value;
    var s = token.Trim();
    var negative = s.StartsWith("-");
    if (negative || s.StartsWith("+"))
      s = s[1..];

    var dot = s.IndexOf('.');
    if (dot >= 0) {
      var frac = s[(dot + 1)..];
      var denom = 1;
      for (var i = 0; i < frac.Length; i++)
        denom *= 10;
      var whole = dot == 0 ? 0 : int.Parse(s.Substring(0, dot));
      var numer = frac.Length == 0 ? 0 : int.Parse(frac);
      value = FP64.FromInt(whole) + FP64.FromInt(numer) / FP64.FromInt(denom);
    }
    else {
      value = FP64.FromInt(int.Parse(s));
    }

    return negative ? -value : value;
  }
}
