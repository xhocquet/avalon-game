using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Assets;

// Instance id is AssetIds.NavigationTuning; look it up with Get<NavigationTuningAsset>().
// Steering/avoidance tuning for NavigationAgentSystem. Distances are authored linearly (squared
// where the system needs them) so the numbers here read as world units.
[KlothoDataAsset(AssetIds.TypeIds.NavigationTuning, AssetId = AssetIds.NavigationTuning,
  Key = "NavigationTuning")]
public partial class NavigationTuningAsset : IDataAsset {
  // Spatial-hash cell size for the ORCA neighbour grids.
  [KlothoOrder(0)] public FP64 AvoidanceGridCellSize;

  // ORCA neighbour query radius for minions. Must be large enough that a minion sees an oncoming
  // minion before they interpenetrate: at MoveSpeed 5 two minions close at up to 10 u/s, so 6 gives
  // ~0.6s of reaction. Too small (was 2) and they only notice each other ~0.2s out — they overlap,
  // react abruptly, and never settle. Heroes use the avoidance runtime's own NeighborDist.
  [KlothoOrder(1)] public FP64 MinionNeighborDist;

  // How far an agent must have moved since its last navmesh snap before we re-snap it.
  [KlothoOrder(2)] public FP64 PositionSnapThreshold;

  // Hard arrival radius for a minion's flow-field destination.
  [KlothoOrder(3)] public FP64 FlowFieldArrivalDist;

  // Inside this radius a minion abandons the flow field and steers straight at its slot.
  [KlothoOrder(4)] public FP64 FlowFieldDirectSteerDist;

  // Ease-in radius: within this distance of the slot the minion decelerates instead of charging at
  // full speed, so it settles into place rather than overshooting and oscillating.
  [KlothoOrder(5)] public FP64 ArrivalBrakeDist;

  // Blocked-settle: after a long move a minion's assigned slot can be unreachable across the packed
  // blob, so it charges the crowd forever (frozen OR oscillating) and the group never stops
  // shuffling. If a minion is within SettleZone of its slot but hasn't gotten meaningfully closer
  // (best distance improved by < SettleProgressStep) for SettleStuckTicks ticks, it gives up and
  // settles in place. The zone gate keeps far-marching minions (waves) from settling when they
  // briefly stall at a chokepoint. Progress-based (not speed-based) so it also catches minions
  // oscillating in place at moderate speed.
  [KlothoOrder(6)] public FP64 SettleZone;
  [KlothoOrder(7)] public FP64 SettleProgressStep;
  [KlothoOrder(8)] public int SettleStuckTicks;

  // Fast-settle: once a minion is inside the destination pile and ORCA has slowed it below
  // BlockedSpeed, it settles immediately rather than inching toward the exact shared point (which
  // makes a crowd compress serially and take ~1.5s to freeze). A freely-approaching minion is
  // moving faster than this until it reaches the hard-arrival radius, so this only catches minions
  // genuinely blocked by the pile.
  [KlothoOrder(9)] public FP64 BlockedZone;
  [KlothoOrder(10)] public FP64 BlockedSpeed;

  // Temporal spreading: only update 1/N of agents per tick for expensive phases.
  // 1 = every tick (no spreading), 2 = every other tick, etc. Phases are offset so they don't
  // spike the same frame. Movement integration and transform sync always run every tick.
  [KlothoOrder(11)] public int HeroSteeringSpread;
  [KlothoOrder(12)] public int MinionSteeringSpread;
  [KlothoOrder(13)] public int AvoidanceSpread;
}
