using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim;

// Triggered when a skill projectile despawns
[KlothoSerializable(117)]
public partial class SkillProjectileDespawnedEvent : SimulationEvent {
  [KlothoOrder(0)] public int ProjectileId;
  [KlothoOrder(1)] public FPVector3 Position;

  // 0 when the projectile expired at the end of its range rather than hitting something.
  [KlothoOrder(2)] public int HitUnitId;
  [KlothoOrder(3)] public int Reason; // SkillProjectileEnd
}
