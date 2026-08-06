using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim;

[KlothoSerializable(107)]
public partial class TurretDestroyedEvent : SimulationEvent {
  [KlothoOrder(1)] public int DestroyerUnitId;
  [KlothoOrder(0)] public int UnitId;
  [KlothoOrder(2)] public FPVector3 Position;

  public override EventMode Mode => EventMode.Synced;
}
