using Meesles.Avalon.Sim.Assets;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Deterministic.Navigation;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Factories;

// Helper for building Klotho's NavAgentComponent at a position, with acceleration derived from
// speed via NavigationTuningAsset.AccelerationFactor.
public static class NavAgentFactory {
  public static NavAgentComponent At(ref Frame frame, FPVector3 position, FP64 speed, FP64 radius) {
    var tuning = frame.AssetRegistry.Get<NavigationTuningAsset>();

    var nav = new NavAgentComponent();
    NavAgentComponent.Init(ref nav, position);
    nav.Speed = speed;
    nav.Acceleration = speed * tuning.AccelerationFactor;
    nav.Radius = radius;
    return nav;
  }
}
