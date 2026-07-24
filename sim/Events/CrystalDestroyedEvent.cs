using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim;

[KlothoSerializable(106)]
public partial class CrystalDestroyedEvent : SimulationEvent {
  [KlothoOrder(1)] public int CrystalId;
  [KlothoOrder(6)] public int DestroyerOwnerId;
  [KlothoOrder(5)] public int DestroyerTeamId;
  [KlothoOrder(4)] public int DestroyerUnitId;
  [KlothoOrder(3)] public int OwnerId;
  [KlothoOrder(2)] public int TeamId;

  [KlothoOrder(0)] public int UnitId;

  // Last known crystal position, so the view can place a death effect even after the entity's
  // pooled view node has been recycled.
  [KlothoOrder(7)] public FPVector3 Position;
  public override EventMode Mode => EventMode.Synced;
}
