using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Assets;

// Instance ids live in the AssetIds.MatchRules* block; look one up with Get<MatchRulesAsset>(id).
[KlothoDataAsset(AssetIds.TypeIds.MatchRules, AssetId = AssetIds.MatchRules, Key = "MatchRules")]
public partial class MatchRulesAsset : IDataAsset {
  [KlothoOrder(0)] public FP64 MatchDuration; // Seconds
  [KlothoOrder(1)] public int SetupGraceTicks; // Faction selection timer
  [KlothoOrder(2)] public int RespawnDelayMs;
  [KlothoOrder(3)] public int DeathScorePenalty;
  [KlothoOrder(4)] public int GoldTickIntervalMs;
  [KlothoOrder(5)] public int StartingGoldPerTick;
}
