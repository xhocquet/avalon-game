using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim;

[KlothoSerializable(115)]
public partial class SkillCastEvent : SimulationEvent {
  [KlothoOrder(0)] public int UnitId;
  [KlothoOrder(1)] public int PlayerId;
  [KlothoOrder(2)] public int Slot;
  [KlothoOrder(3)] public int SkillAssetId;
  [KlothoOrder(4)] public int Rank;

  // Where the hero stood when it cast, so the view can place the effect without a live lookup.
  [KlothoOrder(5)] public FPVector3 Position;
  public override EventMode Mode => EventMode.Synced;
}
