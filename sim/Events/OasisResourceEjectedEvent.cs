using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim;

// Fired when a resource leaves the oasis
[KlothoSerializable(110)]
public partial class OasisResourceEjectedEvent : SimulationEvent {
  [KlothoOrder(0)] public int OasisId;
  [KlothoOrder(1)] public int PickupId;
  [KlothoOrder(2)] public FPVector3 OasisPosition;
  [KlothoOrder(3)] public FPVector3 TargetPosition;
  [KlothoOrder(4)] public int FlightDurationMs;
  [KlothoOrder(5)] public int TypeAssetId;

  public override EventMode Mode => EventMode.Synced;
}
