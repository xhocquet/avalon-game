using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Assets;

[KlothoDataAsset(AssetIds.TypeIds.MovementRules, AssetId = AssetIds.MovementRules, Key = "MovementRules")]
public partial class MovementRulesAsset : IDataAsset {
  [KlothoOrder(0)] public FP64 StopDistance;
  [KlothoOrder(2)] public FP64 HeroClearance;
  [KlothoOrder(3)] public FP64 HeroLateralSpacing;
  [KlothoOrder(4)] public FP64 MoveTargetEdgeClearance;

  [KlothoOrder(1)] public FP64 MinionPackRadiusFactor; // Roughly 0.4 * sqrt(count)
}
