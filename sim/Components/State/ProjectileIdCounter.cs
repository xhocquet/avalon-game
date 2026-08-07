using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// Projectiles need IDs so rollback can account for individual bullet hits
// When doing large bursts/AOE, you only need one 'projectile'
[KlothoComponent(ComponentIds.ProjectileIdCounter)]
[KlothoSingletonComponent]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct ProjectileIdCounter : IComponent {
  public int NextProjectileId;
}
