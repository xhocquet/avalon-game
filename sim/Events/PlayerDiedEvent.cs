using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim {
  [KlothoSerializable(104)]
  public partial class PlayerDiedEvent : SimulationEvent {
    public override EventMode Mode => EventMode.Synced;

    [KlothoOrder(0)] public int PlayerId;
    [KlothoOrder(1)] public int TeamId;
    [KlothoOrder(2)] public int UnitId;
    [KlothoOrder(3)] public FPVector3 Position;
    [KlothoOrder(4)] public int RespawnDelayTicks;
  }
}
