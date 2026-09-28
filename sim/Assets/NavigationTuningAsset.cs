using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Assets;

// NavigationAgentSystem tuning. Distances are authored in world units.
[KlothoDataAsset(AssetIds.TypeIds.NavigationTuning, AssetId = AssetIds.NavigationTuning,
  Key = "NavigationTuning")]
public partial class NavigationTuningAsset : IDataAsset {
  [KlothoOrder(0)] public FP64 AvoidanceGridCellSize; // ORCA spatial-hash cell width

  [KlothoOrder(1)] public FP64 MinionNeighborDist; // Minion ORCA radius; heroes use the runtime default
  [KlothoOrder(2)] public FP64 PositionSnapThreshold;
  [KlothoOrder(3)] public FP64 FlowFieldArrivalDist;
  [KlothoOrder(4)] public FP64 FlowFieldDirectSteerDist;

  [KlothoOrder(5)] public FP64 ArrivalBrakeDist; // Minions brake inside this distance of their slot

  [KlothoOrder(6)] public FP64 SettleZone; // Settle after insufficient progress within this zone
  [KlothoOrder(7)] public FP64 SettleProgressStep;
  [KlothoOrder(8)] public int SettleStuckTicks;

  // Reserved unused fields; do not change their KlothoOrder values.
  [KlothoOrder(9)] public FP64 BlockedZone;
  [KlothoOrder(10)] public FP64 BlockedSpeed;

  // Expensive phases run for one in every N agents per tick.
  [KlothoOrder(11)] public int HeroSteeringSpread;
  [KlothoOrder(12)] public int MinionSteeringSpread;
  [KlothoOrder(13)] public int AvoidanceSpread;

  [KlothoOrder(14)] public FP64 AvoidanceTimeHorizon; // ORCA lookahead in seconds
  [KlothoOrder(15)] public FP64 AccelerationFactor; // Controls
}
