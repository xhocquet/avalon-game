using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Assets;

// Instance id is AssetIds.CombatRules; look it up with Get<CombatRulesAsset>().
[KlothoDataAsset(AssetIds.TypeIds.CombatRules, AssetId = AssetIds.CombatRules, Key = "CombatRules")]
public partial class CombatRulesAsset : IDataAsset {
  // Cell size for the coarse grid, aiming for 3x3 surrounding the current unit
  [KlothoOrder(0)] public FP64 TargetGridCellSize;
}
