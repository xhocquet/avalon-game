using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Assets;

[KlothoDataAsset(AssetIds.TypeIds.CombatRules, AssetId = AssetIds.CombatRules, Key = "CombatRules")]
public partial class CombatRulesAsset : IDataAsset {
  [KlothoOrder(0)] public FP64 TargetGridCellSize; // Target-grid cell width
}
