using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim;

[KlothoSerializable(107)]
public partial class TurretDestroyedEvent : SimulationEvent {
  [KlothoOrder(1)] public int DestroyerUnitId;

  [KlothoOrder(0)] public int UnitId;

  // Last known turret position, so the view can place a death effect even after the entity's
  // pooled view node has been recycled.
  [KlothoOrder(2)] public FPVector3 Position;
  public override EventMode Mode => EventMode.Synced;
}
