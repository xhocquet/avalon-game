using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Deterministic.Navigation;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim {
  public static class NavAgentSetup {
    public static void AddNavAgent(ref Frame frame, EntityRef entity, FPVector3 position, FP64 speed) {
      var nav = new NavAgentComponent();
      NavAgentComponent.Init(ref nav, position);
      nav.Speed = speed;
      nav.Acceleration = speed * FP64.FromInt(12);
      frame.Add(entity, nav);
    }
  }
}
