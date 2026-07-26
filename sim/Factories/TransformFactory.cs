using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Factories;

// Helper for building Klotho's built-in TransformComponent at a position with identity rotation and
// unit scale.
public static class TransformFactory {
  public static TransformComponent At(FPVector3 position) {
    return new TransformComponent {
      Position = position,
      Rotation = FP64.Zero,
      Scale = FPVector3.One
    };
  }
}
