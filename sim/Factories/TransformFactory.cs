using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Factories;

// Helper for building Klotho's built-in TransformComponent at a position with unit scale, and
// either identity rotation or an explicit yaw. Rotation is the Atan2(x, z) yaw convention used by
// CommandSystem and NavigationAgentSystem.
public static class TransformFactory {
  public static TransformComponent At(FPVector3 position) {
    return At(position, FP64.Zero);
  }

  public static TransformComponent At(FPVector3 position, FP64 rotation) {
    return new TransformComponent {
      Position = position,
      Rotation = rotation,
      Scale = FPVector3.One
    };
  }
}
