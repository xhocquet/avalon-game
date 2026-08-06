using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim;

[KlothoSerializable(106)]
public partial class CrystalDestroyedEvent : SimulationEvent {
  [KlothoOrder(0)] public int UnitId;
  [KlothoOrder(1)] public int CrystalId;
  [KlothoOrder(2)] public int TeamId;
  [KlothoOrder(3)] public int DestroyerUnitId;
  [KlothoOrder(4)] public int DestroyerTeamId;
  [KlothoOrder(5)] public FPVector3 Position;

  public override EventMode Mode => EventMode.Synced;
}
