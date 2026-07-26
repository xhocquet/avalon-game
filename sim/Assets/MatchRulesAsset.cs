using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Assets;

// Instance id is AssetIds.MatchRules; look it up with Get<MatchRulesAsset>().
// Match-level flow: how long a match runs, how long setup waits on faction picks, how long a dead
// hero stays down.
[KlothoDataAsset(AssetIds.TypeIds.MatchRules, AssetId = AssetIds.MatchRules, Key = "MatchRules")]
public partial class MatchRulesAsset : IDataAsset {
  // Seconds. ScoreSystem ends the match on the tick this lands on.
  [KlothoOrder(0)] public FP64 MatchDuration;

  // How long HeroSpawnSystem/TeamPruneSystem wait for every player to confirm a faction before
  // proceeding with whatever picks are on the board.
  [KlothoOrder(1)] public int SetupGraceTicks;

  // Wall-clock delay before a dead hero respawns; RespawnSystem converts it to ticks.
  [KlothoOrder(2)] public int RespawnDelayMs;
}
