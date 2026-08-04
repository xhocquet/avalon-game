using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Assets;

// Instance id is AssetIds.MovementRules; look it up with Get<MovementRulesAsset>().
[KlothoDataAsset(AssetIds.TypeIds.MovementRules, AssetId = AssetIds.MovementRules, Key = "MovementRules")]
public partial class MovementRulesAsset : IDataAsset {
  [KlothoOrder(0)] public FP64 StopDistance; // To prevent inching closer
  [KlothoOrder(2)] public FP64 HeroClearance; // Padding between hero and minions
  [KlothoOrder(3)] public FP64 HeroLateralSpacing; // Padding between heroes

  // Approximates the packed-blob radius of N minions (~0.4·sqrt(N) for hex packing near ORCA
  // spacing) so the blob's front edge sits just behind the hero.
  [KlothoOrder(1)] public FP64 MinionPackRadiusFactor;
}
