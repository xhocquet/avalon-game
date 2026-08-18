using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim;

[KlothoSerializable(118)]
public partial class AttackProcConsumedEvent : SimulationEvent {
  [KlothoOrder(0)] public int AttackHitId; // FK to AttackHitEvent.AttackHitId
  [KlothoOrder(1)] public int AttackerUnitId;
  [KlothoOrder(2)] public int TargetUnitId;
  [KlothoOrder(3)] public int SkillAssetId;
  [KlothoOrder(4)] public FP64 DamageMultiplier; // What this proc multiplied the hit by
}
