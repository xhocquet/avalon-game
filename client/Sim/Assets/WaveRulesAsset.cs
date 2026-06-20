using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon {
  // Tunable minion-wave rules. Crank MinionsPerWave / shrink SpawnIntervalTicks to
  // stress-test how many deterministic minion entities the netcode handles (Milestone M).
  [KlothoDataAsset(101, AssetId = 101, Key = "WaveRules")]
  public partial class WaveRulesAsset : IDataAsset {
    [KlothoOrder(0)] public int FirstWaveDelayTicks;
    [KlothoOrder(1)] public int SpawnIntervalTicks;
    [KlothoOrder(2)] public int MinionsPerWave;
    [KlothoOrder(3)] public int MinionHealth;
    [KlothoOrder(4)] public FP64 MinionMass;
    [KlothoOrder(5)] public FP64 MinionHalfExtent;
    [KlothoOrder(6)] public FP64 MinionSpacing;
    // Distance to push each wave out from the base toward map center (the lane direction),
    // so minions spawn in front of the base instead of inside the base mesh.
    [KlothoOrder(8)] public FP64 SpawnForwardOffset;
    // Minion march speed toward center, units/sec (Milestone M3).
    [KlothoOrder(9)] public FP64 MinionMoveSpeed;
    // Hard ceiling on live minions. Until combat/death (Milestone A) removes them,
    // minions accumulate forever — this keeps the world under MaxEntities so the sim
    // doesn't blow the entity pool. Raise alongside MaxEntities to push the stress test.
    [KlothoOrder(7)] public int MaxConcurrentMinions;
    [KlothoOrder(10)] public int MinionAttackDamage;
    [KlothoOrder(11)] public FP64 MinionAttackRange;
    [KlothoOrder(12)] public int MinionAttackCooldownTicks;
  }
}
