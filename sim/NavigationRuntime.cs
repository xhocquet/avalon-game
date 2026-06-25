using xpTURN.Klotho.Deterministic.Navigation;
using xpTURN.Klotho.Logging;

namespace Meesles.Avalon.Sim {
  public sealed class NavigationRuntime {
    public FPNavMesh NavMesh { get; }
    public FPNavMeshQuery Query { get; }
    public FPNavMeshPathfinder Pathfinder { get; }
    public FPNavMeshFunnel Funnel { get; }
    public FPNavAvoidance Avoidance { get; }
    public FPNavAgentSystem AgentSystem { get; }

    private NavigationRuntime(FPNavMesh navMesh, FPNavMeshQuery query, FPNavMeshPathfinder pathfinder,
      FPNavMeshFunnel funnel, FPNavAvoidance avoidance, FPNavAgentSystem agentSystem) {
      NavMesh = navMesh;
      Query = query;
      Pathfinder = pathfinder;
      Funnel = funnel;
      Avoidance = avoidance;
      AgentSystem = agentSystem;
    }

    public static NavigationRuntime FromBytes(byte[] bytes, IKLogger logger) {
      var navMesh = FPNavMeshSerializer.Deserialize(bytes);
      var query = new FPNavMeshQuery(navMesh, logger);
      var pathfinder = new FPNavMeshPathfinder(navMesh, query, logger);
      var funnel = new FPNavMeshFunnel(navMesh, query, logger);
      var avoidance = new FPNavAvoidance();
      var agentSystem = new FPNavAgentSystem(navMesh, query, pathfinder, funnel, logger);
      agentSystem.SetAvoidance(avoidance);

      return new NavigationRuntime(navMesh, query, pathfinder, funnel, avoidance, agentSystem);
    }
  }
}
