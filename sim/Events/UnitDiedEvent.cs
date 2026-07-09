using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim;

[KlothoSerializable(102)]
public partial class UnitDiedEvent : SimulationEvent {
  [KlothoOrder(2)] public FPVector3 Position;

  [KlothoOrder(0)] public int UnitId;
  [KlothoOrder(1)] public int UnitTypeId;
  public override EventMode Mode => EventMode.Synced;
}
