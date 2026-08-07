using System.Runtime.InteropServices;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// A skill projectile in flight, advanced and collision-checked by ProjectileSystem.
[KlothoComponent(ComponentIds.Projectile)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct Projectile : IComponent {
  public int ProjectileId;
  public int SourceUnitId;
  public int TeamId;
  public int Damage;
  public int SkillAssetId;
  public int Slot;
  public int Index; // Position within its volley
  public FPVector3 Direction; // Normalized and planar (y = 0).
  public FP64 Speed;
  public FP64 RemainingDistance;
  public FP64 Radius; // Half the bullet's width
}
