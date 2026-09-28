using xpTURN.Klotho.Deterministic.Math;

namespace Meesles.Avalon.Sim.Assets;

// Level-1 stats copied into Stats by unit factories. Distances are metres; regen is per five seconds.
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
  FP64 AttackWindup { get; } // Seconds between a swing starting and its damage landing.
  FP64 GameplayRadius { get; } // What a hit tests against, and what AttackRange is measured from
  FP64 PathingRadius { get; } // What the nav agent occupies
}
