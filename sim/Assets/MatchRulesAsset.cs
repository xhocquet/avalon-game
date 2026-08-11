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
  [KlothoOrder(6)] public int HeroKillScore;
  [KlothoOrder(7)] public int MinionKillScore;
  [KlothoOrder(8)] public int StructureKillScore;

  // How often health/mana regen pays out. Regen stats are authored per 5 seconds, so a tick here
  // grants RegenInterval/5000 of the stat. No system reads it yet.
  [KlothoOrder(9)] public int RegenIntervalMs;
}
