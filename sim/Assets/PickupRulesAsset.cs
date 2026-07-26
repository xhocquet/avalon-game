using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Assets;

// Instance id is AssetIds.PickupRules; look it up with Get<PickupRulesAsset>().
// The whole resource loop: how often an oasis coughs up a resource, how the ejection animates, and
// how close a unit has to be to collect what lands.
[KlothoDataAsset(AssetIds.TypeIds.PickupRules, AssetId = AssetIds.PickupRules, Key = "PickupRules")]
public partial class PickupRulesAsset : IDataAsset {
  // Cadence of oasis spawns. Also seeds each oasis's initial cooldown at world init.
  [KlothoOrder(0)] public int OasisSpawnIntervalMs;

  // Wind-up before the resource leaves the oasis (the client plays a tell over this window).
  [KlothoOrder(1)] public int OasisPrepareDurationMs;

  // Flight time from the oasis to the landing spot.
  [KlothoOrder(2)] public int OasisFlightDurationMs;

  // Resources granted by one oasis-spawned pickup. Map-authored pickups carry their own amount.
  [KlothoOrder(3)] public int OasisResourceAmount;

  // Ring radius around the oasis that ejected resources land on; the angle is random per spawn.
  [KlothoOrder(4)] public FP64 OasisEjectRadius;

  // Global cap on uncollected pickups. Oases skip their spawn while the map is at the cap.
  [KlothoOrder(5)] public int MaxGroundPickups;

  // How close a unit with an Inventory must get to sweep up a pickup.
  [KlothoOrder(6)] public FP64 CollectRange;
}
