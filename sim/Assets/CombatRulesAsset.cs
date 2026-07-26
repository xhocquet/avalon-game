using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Assets;

// Instance id is AssetIds.CombatRules; look it up with Get<CombatRulesAsset>().
[KlothoDataAsset(AssetIds.TypeIds.CombatRules, AssetId = AssetIds.CombatRules, Key = "CombatRules")]
public partial class CombatRulesAsset : IDataAsset {
  // Broad-phase cell size for TargetAcquisitionSystem's candidate grid. Wants to be on the order of
  // typical acquisition radii (minion AttackRange * reacquire multiplier, turret AttackRange) so a
  // query spans roughly a 3x3 cell neighbourhood instead of scanning every unit on the map.
  [KlothoOrder(0)] public FP64 TargetGridCellSize;
}
