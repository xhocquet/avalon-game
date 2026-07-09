using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim;

[KlothoSerializable(105)]
public partial class PlayerRespawnedEvent : SimulationEvent {
  [KlothoOrder(0)] public int PlayerId;
  [KlothoOrder(3)] public FPVector3 Position;
  [KlothoOrder(1)] public int TeamId;
  [KlothoOrder(2)] public int UnitId;
  public override EventMode Mode => EventMode.Synced;
}
