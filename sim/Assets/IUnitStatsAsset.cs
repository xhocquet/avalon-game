using xpTURN.Klotho.Deterministic.Math;

namespace Meesles.Avalon.Sim.Assets;

// The level-1 stat block every unit's asset carries, so factories build Stats once instead
// of once per asset type. Implemented explicitly since the serialized members are fields.
//
// Distances are metres: game units divide by 100 (a 350 move speed is 3.5). Regen is per 5 seconds,
// the unit it is authored and displayed in.
//
// Growth past level 1 is not here - only heroes level, and their per-level values live on HeroAsset.
public interface IUnitStatsAsset {
  FP64 BaseHealth { get; }
  FP64 BaseMana { get; }
  FP64 BaseHealthRegen { get; }
  FP64 BaseManaRegen { get; }
  FP64 BaseArmor { get; }
  FP64 BaseMagicResist { get; }
  FP64 BaseAttackDamage { get; }
  FP64 BaseAttackSpeed { get; } // Attacks per second
  FP64 CritChance { get; }
  FP64 CritDamage { get; }
  FP64 MoveSpeed { get; }
  FP64 AttackRange { get; }
  FP64 AcquisitionRange { get; }

  // Fraction of the attack cycle spent winding up before the hit lands. No system reads it yet.
  FP64 AttackWindup { get; }

  FP64 GameplayRadius { get; } // What a hit tests against, and what AttackRange is measured from
  FP64 PathingRadius { get; } // What the nav agent occupies
}
