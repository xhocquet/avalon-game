using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim;

[KlothoSerializable(113)]
public partial class HeroLeveledUpEvent : SimulationEvent {
  [KlothoOrder(0)] public int UnitId;
  [KlothoOrder(1)] public int PlayerId;
  [KlothoOrder(2)] public int Level;

  // Where the hero stood when it levelled, so the view can place the effect without a live lookup.
  [KlothoOrder(3)] public FPVector3 Position;
  public override EventMode Mode => EventMode.Synced;
}
