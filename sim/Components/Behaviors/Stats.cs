using System.Runtime.InteropServices;
using Meesles.Avalon.Sim.Assets;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// Every buffable value a unit carries. Changes apply only to this unit; base values come from the
// unit's asset row and everything on top of them arrives through Add.
//
// Stored as one FP64-per-StatType buffer rather than named fields, so a stat that gains an enum
// value cannot be forgotten in a switch and every write goes through the same clamp. FP64 has no
// blittable fixed-buffer form, so the raw 32.32 longs are the storage and Get/Set convert; the
// generated codec walks the buffer for serialization and hashing the same way Skills does.
// Size: StatCount * 8 = 128B, at the 128-byte component ceiling.
[KlothoComponent(ComponentIds.Stats)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public unsafe partial struct Stats : IComponent {
  private fixed long _values[StatRanges.Count];

  public readonly FP64 MaxHealth => Get(StatType.MaxHealth);
  public readonly FP64 MaxMana => Get(StatType.MaxMana);
  public readonly FP64 HealthRegen => Get(StatType.HealthRegen);
  public readonly FP64 ManaRegen => Get(StatType.ManaRegen);
  public readonly FP64 Armor => Get(StatType.Armor);
  public readonly FP64 MagicResist => Get(StatType.MagicResist);
  public readonly FP64 AttackDamage => Get(StatType.AttackDamage);
  public readonly FP64 BaseAttackSpeed => Get(StatType.BaseAttackSpeed);
  public readonly FP64 BonusAttackSpeed => Get(StatType.BonusAttackSpeed);
  public readonly FP64 CritChance => Get(StatType.CritChance);
  public readonly FP64 CritDamage => Get(StatType.CritDamage);
  public readonly FP64 MoveSpeed => Get(StatType.MoveSpeed);
  public readonly FP64 AttackRange => Get(StatType.AttackRange);
  public readonly FP64 AcquisitionRange => Get(StatType.AcquisitionRange);
  public readonly FP64 GameplayRadius => Get(StatType.GameplayRadius);
  public readonly FP64 AttackWindup => Get(StatType.AttackWindup);

  // Attacks per second, the form DamageSystem needs. Capped because the cooldown is its reciprocal.
  public readonly FP64 AttacksPerSecond {
    get {
      var rate = BaseAttackSpeed * (FP64.One + BonusAttackSpeed);
      return rate < AttackSpeedFloor ? AttackSpeedFloor : rate > AttackSpeedCap ? AttackSpeedCap : rate;
    }
  }

  private static readonly FP64 AttackSpeedCap = FP64.FromInt(5) / FP64.FromInt(2);
  private static readonly FP64 AttackSpeedFloor = FP64.One / FP64.FromInt(10);

  public readonly FP64 Get(StatType stat) => FP64.FromRaw(_values[(int)stat]);

  public void Set(StatType stat, FP64 value) =>
    _values[(int)stat] = StatRanges.Clamp(stat, value).RawValue;

  public void Add(StatType stat, FP64 delta) => Set(stat, Get(stat) + delta);

  // Chainable Set on a copy, so a caller building a block by hand reads like the object initializer
  // the buffer took away: Stats.Create().With(StatType.MoveSpeed, speed).
  public readonly Stats With(StatType stat, FP64 value) {
    var copy = this;
    copy.Set(stat, value);
    return copy;
  }

  // A default-constructed component is all zeroes, which is out of range for anything with a
  // non-zero floor - a BaseAttackSpeed of 0 would divide by zero in the cooldown. Every construction
  // path starts here rather than from `default`.
  public static Stats Create() {
    var stats = new Stats();
    for (var i = 0; i < StatRanges.Count; i++)
      stats._values[i] = StatRanges.Of((StatType)i).Initial.RawValue;

    return stats;
  }

  // Seeds the level-1 values off an asset row. Growth beyond level 1 is applied by ExperienceSystem
  // through Add, so this is only ever the spawn seed.
  public static Stats From(IUnitStatsAsset asset) {
    var stats = Create();
    stats.Set(StatType.MaxHealth, asset.BaseHealth);
    stats.Set(StatType.MaxMana, asset.BaseMana);
    stats.Set(StatType.HealthRegen, asset.BaseHealthRegen);
    stats.Set(StatType.ManaRegen, asset.BaseManaRegen);
    stats.Set(StatType.Armor, asset.BaseArmor);
    stats.Set(StatType.MagicResist, asset.BaseMagicResist);
    stats.Set(StatType.AttackDamage, asset.BaseAttackDamage);
    stats.Set(StatType.BaseAttackSpeed, asset.BaseAttackSpeed);
    stats.Set(StatType.CritChance, asset.CritChance);
    stats.Set(StatType.CritDamage, asset.CritDamage);
    stats.Set(StatType.MoveSpeed, asset.MoveSpeed);
    stats.Set(StatType.AttackRange, asset.AttackRange);
    stats.Set(StatType.AcquisitionRange, asset.AcquisitionRange);
    stats.Set(StatType.GameplayRadius, asset.GameplayRadius);
    stats.Set(StatType.AttackWindup, asset.AttackWindup);
    return stats;
  }
}
