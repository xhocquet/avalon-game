using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Assets;

// Instance id is AssetIds.PickupRules; look it up with Get<PickupRulesAsset>().
[KlothoDataAsset(AssetIds.TypeIds.PickupRules, AssetId = AssetIds.PickupRules, Key = "PickupRules")]
public partial class PickupRulesAsset : IDataAsset {
  [KlothoOrder(0)] public int OasisSpawnIntervalMs; // How often pickups spawn
  [KlothoOrder(1)] public int OasisPrepareDurationMs; // Wind-up until pickup is released
  [KlothoOrder(2)] public int OasisFlightDurationMs; // How long pickup is in the air
  [KlothoOrder(3)] public int OasisResourceAmount; // How many resources in one oasis pickup
  [KlothoOrder(4)] public FP64 OasisEjectRadius; // How far pickups are spawned
  [KlothoOrder(5)] public int MaxGroundPickups;
  [KlothoOrder(6)] public FP64 CollectRange; // Pickup distance for units
}
