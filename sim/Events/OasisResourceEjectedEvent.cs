using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim;

// Fired the instant the resource actually leaves the oasis. FlightDurationMs is how long the
// view has to animate the resource travelling from OasisPosition to TargetPosition before it lands.
[KlothoSerializable(110)]
public partial class OasisResourceEjectedEvent : SimulationEvent {
  [KlothoOrder(0)] public int OasisId;
  [KlothoOrder(1)] public int PickupId;
  [KlothoOrder(2)] public FPVector3 OasisPosition;
  [KlothoOrder(3)] public FPVector3 TargetPosition;
  [KlothoOrder(4)] public int FlightDurationMs;
  public override EventMode Mode => EventMode.Synced;
}
