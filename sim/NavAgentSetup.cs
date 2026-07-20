using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Deterministic.Navigation;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

public static class NavAgentSetup {
  // Collision/ORCA radius per unit class. Minions are ~0.6m wide (e.g. SwirlyEye) so 0.3
  // matches their visual footprint; heroes are chunkier. NavAgentComponent.Init defaults to
  // 0.5, so every agent must set this explicitly after Init.
  public static readonly FP64 HeroRadius = FP64.FromDouble(0.5);
  public static readonly FP64 MinionRadius = FP64.FromDouble(0.3);

  public static void AddNavAgent(ref Frame frame, EntityRef entity, FPVector3 position, FP64 speed, FP64 radius) {
    var nav = new NavAgentComponent();
    NavAgentComponent.Init(ref nav, position);
    nav.Speed = speed;
    nav.Acceleration = speed * FP64.FromInt(12);
    nav.Radius = radius;
    frame.Add(entity, nav);
  }
}
