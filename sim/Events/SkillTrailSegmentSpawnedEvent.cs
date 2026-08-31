using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim;

// Triggered when a laid trail drops one segment. The segment always lives exactly LifetimeTicks and
// has no early end, so the view times its own despawn off this - there is no matching despawn event.
[KlothoSerializable(122)]
public partial class SkillTrailSegmentSpawnedEvent : SimulationEvent {
  [KlothoOrder(0)] public int SegmentId;
  [KlothoOrder(1)] public int SourceUnitId;
  [KlothoOrder(2)] public int SkillAssetId;
  [KlothoOrder(3)] public FPVector3 Position;
  [KlothoOrder(4)] public FP64 Width;
  [KlothoOrder(5)] public int LifetimeTicks;
}
