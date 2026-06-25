using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Assets {
  [KlothoDataAsset(101, AssetId = 101, Key = "WaveRules")]
  public partial class WaveRulesAsset : IDataAsset {
    [KlothoOrder(0)] public int FirstWaveDelayTicks;
    [KlothoOrder(1)] public int SpawnIntervalTicks;
    [KlothoOrder(2)] public int MinionsPerWave;
    [KlothoOrder(3)] public FP64 MinionSpacing;
  }
}
