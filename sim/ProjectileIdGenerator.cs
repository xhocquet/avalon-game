using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

// Single global sequence for Projectile.ProjectileId, shared by every skill that fires one, so the
// spawn and despawn events a client pairs up never collide across casters.
public static class ProjectileIdGenerator {
  public const int FirstProjectileId = 1;

  public static void Initialize(ref Frame frame, int nextProjectileId = FirstProjectileId) {
    if (frame.TryGetSingleton<ProjectileIdCounter>(out _)) return;

    var entity = frame.CreateEntity();
    frame.Add(entity, new ProjectileIdCounter { NextProjectileId = nextProjectileId });
  }

  public static int Next(ref Frame frame) {
    Initialize(ref frame);

    ref var state = ref frame.GetSingleton<ProjectileIdCounter>();
    var projectileId = state.NextProjectileId;
    state.NextProjectileId += 1;
    return projectileId;
  }
}
