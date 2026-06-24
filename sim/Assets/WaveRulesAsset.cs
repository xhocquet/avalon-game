using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Assets {
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

    [KlothoOrder(9)] public FP64 MinionMoveSpeed;

    [KlothoOrder(10)] public int MinionAttackDamage;
    [KlothoOrder(11)] public FP64 MinionAttackRange;
    [KlothoOrder(12)] public int MinionAttackCooldownTicks;
  }
}
