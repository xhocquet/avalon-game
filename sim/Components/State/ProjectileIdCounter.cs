using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// Projectiles need IDs so rollback can account for individual bullet hits
// When doing large bursts/AOE, you only need one 'projectile'
// Shared by every skill that fires one, so the spawn and despawn events a client pairs up never
// collide across casters.
[KlothoComponent(ComponentIds.ProjectileIdCounter)]
[KlothoSingletonComponent]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct ProjectileIdCounter : IComponent, IIdCounter {
  public int NextProjectileId;

  public int NextId { readonly get => NextProjectileId; set => NextProjectileId = value; }
}
