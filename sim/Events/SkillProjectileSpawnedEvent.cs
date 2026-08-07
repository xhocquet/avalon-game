using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim;

// Triggered when a skill projectile spawns
[KlothoSerializable(116)]
public partial class SkillProjectileSpawnedEvent : SimulationEvent {
  [KlothoOrder(0)] public int ProjectileId;
  [KlothoOrder(1)] public int SourceUnitId;
  [KlothoOrder(2)] public int SkillAssetId;
  [KlothoOrder(3)] public int Slot;
  [KlothoOrder(4)] public int Index; // Position within the volley
  [KlothoOrder(5)] public FPVector3 Origin;
  [KlothoOrder(6)] public FPVector3 Direction;
  [KlothoOrder(7)] public FP64 Speed;
  [KlothoOrder(8)] public FP64 Range;
  [KlothoOrder(9)] public FP64 Radius;
}
