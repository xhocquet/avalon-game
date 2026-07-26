using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Assets;

// Shared shop interaction rules. The sim uses this as the authoritative purchase gate (see
// CommandSystem.HandlePurchaseItemCommand); the client uses the same asset to decide when to show
// the shop's buy actions, so the UI hint and the authoritative check never disagree.
[KlothoDataAsset(108, AssetId = 108, Key = "ShopRules")]
public partial class ShopRulesAsset : IDataAsset {
  // Max planar (XZ) distance in world metres between a hero and its team's Shop marker for a
  // purchase to be allowed.
  [KlothoOrder(0)] public FP64 InteractRange;
}
