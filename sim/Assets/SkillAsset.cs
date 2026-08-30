using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Assets;

// Instance ids live in the AssetIds.Skill* block; look one up with Get<SkillAsset>(id)
[KlothoDataAsset(AssetIds.TypeIds.Skill)]
public partial class SkillAsset : IDataAsset {
  [KlothoOrder(0)] public int MaxRank = 4; // Omitted in JSON means 4
  [KlothoOrder(1)] public int CooldownMs;
  [KlothoOrder(2)] public FP64 Damage;
  [KlothoOrder(3)] public FP64 DamagePerRank;
  [KlothoOrder(4)] public FP64 ProjectileSpeed; // Units/second
  [KlothoOrder(5)] public FP64 ProjectileRange;
  [KlothoOrder(6)] public FP64 ProjectileRadius;
  [KlothoOrder(7)] public int ProjectileCount;
  [KlothoOrder(8)] public FP64 ProjectileSpacing;
  [KlothoOrder(9)] public FP64 ProjectileSpawnOffset; // Range is measured from the offset position

  // Cast band around the caster, both optional: omitted in JSON (0) means unbounded on that end.
  // SkillAim.ClampToCastRange pulls the aim point onto the band before the effect runs.
  [KlothoOrder(10)] public FP64 MinCastRange;
  [KlothoOrder(11)] public FP64 MaxCastRange;

  // Timed stat buff block. BuffPercent is the fraction of the stat added at rank 1 and
  // BuffPercentPerRank the step each rank after; which stats it lands on is the skill's own business.
  [KlothoOrder(12)] public int BuffDurationMs;
  [KlothoOrder(13)] public FP64 BuffPercent;
  [KlothoOrder(14)] public FP64 BuffPercentPerRank;

  // Empowered-attack block. ProcDamageMultiplier is the total multiplier the armed attack lands with
  // (4 is 400% of a normal hit) and ProcDurationMs how long the charge waits to be spent.
  [KlothoOrder(15)] public int ProcDurationMs;
  [KlothoOrder(16)] public FP64 ProcDamageMultiplier;
  [KlothoOrder(17)] public FP64 ProcDamageMultiplierPerRank;

  // 0/1. Arming clears the caster's attack cooldown, so the empowered swing lands at once instead of
  // waiting out the auto-attack that came before it. Per-row because not every proc wants it.
  [KlothoOrder(18)] public int ProcResetsAttackCooldown;

  // Heal block, authored as a fraction of the target's MaxHealth rather than a flat number so it keeps
  // its meaning as the pool grows with level. HealPercent is rank 1, HealPercentPerRank the step after.
  [KlothoOrder(19)] public FP64 HealPercent;
  [KlothoOrder(20)] public FP64 HealPercentPerRank;

  // 0/1. The skill has no aim: it resolves on the caster and the aim point is replaced with the
  // caster's own position, so nothing downstream has to decide whether the target means anything. The
  // client reads the same flag to fire on key-down instead of holding the key to aim.
  [KlothoOrder(21)] public int SelfCast;

  // Attack-burst block. BurstAttackCount is how many auto-attacks the cast is worth in total (2 is a
  // double swing), BurstAttackDelayMs the spacing between them, and BurstDurationMs how long the
  // queued swings wait for a target before they lapse.
  [KlothoOrder(22)] public int BurstAttackCount;
  [KlothoOrder(23)] public int BurstAttackCountPerRank;
  [KlothoOrder(24)] public int BurstAttackDelayMs;
  [KlothoOrder(25)] public int BurstDurationMs;

  // 0/1. Queuing clears the caster's swing timer, so the burst opens on the cast tick instead of
  // waiting out the auto-attack that came before it. Same field as ProcResetsAttackCooldown, for the
  // other lifecycle.
  [KlothoOrder(26)] public int BurstResetsAttackCooldown;

  // Cone block. ConeRange is how far the wedge reaches from the caster's centre and ConeAngleDegrees
  // its full opening angle; what it does to what it catches is the skill's own business, and its
  // damage comes off the shared Damage/DamagePerRank pair.
  [KlothoOrder(27)] public FP64 ConeRange;
  [KlothoOrder(28)] public FP64 ConeAngleDegrees;

  // Tooltip text. The only non-tuning field on the row, kept here rather than in the client's
  // SkillCatalog so the words and the numbers they describe are authored in one place. Nothing in the
  // sim reads it; null and "" are the same to the codec, so an unauthored row costs 4 bytes.
  [KlothoOrder(29)] public string Description;

  // Area block. AreaRadius is the reach of a disc centred on the caster, and SnareDurationMs how long
  // whatever it catches is held in place; the damage comes off the shared Damage/DamagePerRank pair.
  // Which of the two a skill uses is its own business - a hold with no damage is a legal row.
  [KlothoOrder(30)] public FP64 AreaRadius;
  [KlothoOrder(31)] public int SnareDurationMs;
  [KlothoOrder(32)] public int SnareDurationMsPerRank;

  // How long a charged skill spends winding up before its area pays out, and whether the caster is
  // rooted for that wind-up. The buff block times the channel alongside it, so an authored row keeps
  // ChargeDurationMs and BuffDurationMs the same unless the buff is meant to outlast the hold.
  [KlothoOrder(33)] public int ChargeDurationMs;
  [KlothoOrder(34)] public int ChargeRootsCaster;

  // Damage-over-time block. A lingering effect burns its target for DotDamagePerSecond (+PerRank)
  // magical damage, paid tick by tick at the per-second rate scaled to the tick length. DotDurationMs
  // is the window for an effect that owns its own clock (a debuff left by a projectile hit); a channel
  // takes only the rate and runs it for ChargeDurationMs.
  [KlothoOrder(35)] public FP64 DotDamagePerSecond;
  [KlothoOrder(36)] public FP64 DotDamagePerSecondPerRank;
  [KlothoOrder(37)] public int DotDurationMs;

  // Which stats a timed stat buff lands on, one ';'-separated entry per stat, resolved through
  // BuffSpecs. Two forms, mixable in one row:
  //   "Armor,MagicResist"                 - bare name(s), each on the scalar BuffPercent pair, pct mode
  //   "MoveSpeed pct 0.15 0.05"           - <StatName> <pct|flat> <base> [perRank]
  //   "MoveSpeed pct 0.15 0.05; BonusAttackSpeed flat 0.10 0.10; Armor pct -0.20 -0.05"
  // pct adds a fraction of the stat's current value, flat adds the number as-is. The effect code owns
  // whether it hits the caster or the target; the sign is authored here.
  [KlothoOrder(38)] public string BuffStats;

  // Flat heal block, the sibling of the percent pair: a fixed amount rather than a fraction of the
  // target's pool. HealAmount is rank 1, HealAmountPerRank the step after. A row authors one form or
  // the other.
  [KlothoOrder(39)] public FP64 HealAmount;
  [KlothoOrder(40)] public FP64 HealAmountPerRank;

  // Per-rank step on the stat-buff window, for a row whose buff outlasts its rank-1 duration by more
  // each level. BuffDurationMs stays the rank-1 value; omitted (0) means the window does not grow.
  [KlothoOrder(41)] public int BuffDurationMsPerRank;

  // How far a dash carries its caster along the aim direction, from the cast position. Kept off the
  // tooltip on purpose. 0 means the row does not dash.
  [KlothoOrder(42)] public FP64 DashDistance;

  // How long whatever an effect catches is silenced - no casts for the window. Authored like
  // SnareDurationMs and independent of it; a row can do both, one, or neither.
  [KlothoOrder(43)] public int SilenceDurationMs;

  // Ground-trail block. A trail laid behind the moving caster lingers for TrailDurationMs (+PerRank)
  // and catches anything within TrailWidth of it; what it does to a hit comes off the buff block (a
  // MoveSpeed slow, say). TrailWidth is not in the tooltip. 0 duration means the row lays no trail.
  [KlothoOrder(44)] public int TrailDurationMs;
  [KlothoOrder(45)] public int TrailDurationMsPerRank;
  [KlothoOrder(46)] public FP64 TrailWidth;

  // Per-rank step on the channel wind-up, negative for a row that charges faster each rank. Floored at
  // 0 by ChargeDurationMsAtRank. ChargeDurationMs stays the rank-1 value.
  [KlothoOrder(47)] public int ChargeDurationMsPerRank;

  // 0/1. The channel breaks if its caster moves, rather than rooting them for the wind-up. Mutually
  // exclusive with ChargeRootsCaster in practice - a rooted caster cannot move to break it.
  [KlothoOrder(48)] public int ChargeCancelsOnMove;

  // 0/1. The cast strips negative statuses off its target. Authored where the design calls for a
  // cleanse; the effect that reads it is pending a debuff pipeline to clear.
  [KlothoOrder(49)] public int ClearsDebuffs;

  // Multi-dash block. DashCount (+PerRank) is how many leaps the cast is worth, each carrying the
  // caster DashDistance along its aim line and dealing the shared Damage/DamagePerRank to what it
  // passes through. 0 means a single dash (or none, if DashDistance is also 0).
  [KlothoOrder(50)] public int DashCount;
  [KlothoOrder(51)] public int DashCountPerRank;

  // Stockpile block. The skill banks up to StockpileMax uses, gaining one every StockpileIntervalMs
  // (+PerRank, negative to accrue faster each rank); a cast spends one from the bank. Independent of
  // CooldownMs, which still gates how fast banked uses can be spent.
  [KlothoOrder(52)] public int StockpileMax;
  [KlothoOrder(53)] public int StockpileIntervalMs;
  [KlothoOrder(54)] public int StockpileIntervalMsPerRank;

  // Mana restored to the target, the resource sibling of the heal block. Applied through
  // ManaApplication.Restore, clamped to the target's Stats.MaxMana headroom.
  [KlothoOrder(55)] public FP64 ManaRestore;
  [KlothoOrder(56)] public FP64 ManaRestorePerRank;

  // Mana spent to cast, the resource sibling of CooldownMs. ManaCost is rank 1, ManaCostPerRank the
  // step each rank after (authored positive even where a deeper rank costs more). 0 means the cast is
  // free. SkillActions checks the pool in EvaluateCast and spends on a successful TryCast, after the
  // cooldown starts, so a rejected cast never pays.
  [KlothoOrder(57)] public FP64 ManaCost;
  [KlothoOrder(58)] public FP64 ManaCostPerRank;

  private BuffSpec[] _buffSpecs;

  public bool HasCastRange => MinCastRange > FP64.Zero || MaxCastRange > FP64.Zero;
  public bool IsSelfCast => SelfCast != 0;
  public bool HasCone => ConeRange > FP64.Zero && ConeAngleDegrees > FP64.Zero;
  public bool HasArea => AreaRadius > FP64.Zero;
  public bool HasDash => DashDistance > FP64.Zero;
  public bool HasStockpile => StockpileMax > 0;
  public bool HasTrail => TrailDurationMs > 0;
  public bool ChargeRootsItsCaster => ChargeRootsCaster != 0;
  public bool ChannelBreaksOnMove => ChargeCancelsOnMove != 0;
  public bool ClearsItsTargetsDebuffs => ClearsDebuffs != 0;

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
      if (tokens.Length == 1) { // bare name, or a comma list of them, on the scalar BuffPercent pair
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

  public FP64 ProcDamageMultiplierAtRank(int rank) {
    return rank <= 0
      ? FP64.Zero
      : ProcDamageMultiplier + ProcDamageMultiplierPerRank * FP64.FromInt(rank - 1);
  }
}
