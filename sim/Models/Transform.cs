using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Models;

public static class Transform {
  public static TransformComponent At(FPVector3 position) {
    return new TransformComponent {
      Position = position,
      Rotation = FP64.Zero,
      Scale = FPVector3.One
    };
  }
}
