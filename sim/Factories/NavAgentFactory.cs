using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Deterministic.Navigation;

namespace Meesles.Avalon.Sim.Factories;

// Helper for building Klotho's NavAgentComponent at a position, with acceleration derived from
// speed so units reach full speed in ~1/12s.
public static class NavAgentFactory {
  public static NavAgentComponent At(FPVector3 position, FP64 speed, FP64 radius) {
    var nav = new NavAgentComponent();
    NavAgentComponent.Init(ref nav, position);
    nav.Speed = speed;
    nav.Acceleration = speed * FP64.FromInt(12);
    nav.Radius = radius;
    return nav;
  }
}
