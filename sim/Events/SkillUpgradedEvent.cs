using xpTURN.Klotho.Core;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim;

[KlothoSerializable(114)]
public partial class SkillUpgradedEvent : SimulationEvent {
  [KlothoOrder(0)] public int UnitId;
  [KlothoOrder(1)] public int PlayerId;
  [KlothoOrder(2)] public int Slot;
  [KlothoOrder(3)] public int SkillAssetId;
  [KlothoOrder(4)] public int NewRank;

  // Points left after the spend, so the view can redraw the tree without re-reading the frame.
  [KlothoOrder(5)] public int RemainingPoints;
  public override EventMode Mode => EventMode.Synced;
}
