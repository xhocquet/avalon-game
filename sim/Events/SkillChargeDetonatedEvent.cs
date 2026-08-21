using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim;

// Triggered when a charged skill burst pays out, one per detonation. Position is where the disc was
// centred, which is wherever the caster stood on the detonation tick rather than where it cast.
[KlothoSerializable(119)]
public partial class SkillChargeDetonatedEvent : SimulationEvent {
  [KlothoOrder(0)] public int CasterUnitId;
  [KlothoOrder(1)] public int SkillAssetId;
  [KlothoOrder(2)] public FPVector3 Position;
  [KlothoOrder(3)] public FP64 Radius;
  [KlothoOrder(4)] public int HitCount;
}
