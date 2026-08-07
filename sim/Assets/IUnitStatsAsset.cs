using xpTURN.Klotho.Deterministic.Math;

namespace Meesles.Avalon.Sim.Assets;

// The stat block every attacking unit's asset carries, so factories build Combat/StatsComponent once
// instead of once per asset type. Implemented explicitly since the serialized members are fields.
public interface IUnitStatsAsset {
  int Health { get; }
  int AttackDamage { get; }
  int Defense { get; }
  int AttackCooldownTicks { get; }
  FP64 MoveSpeed { get; }
  FP64 AttackRange { get; }
  FP64 AttackReacquireRangeMultiplier { get; }
}
