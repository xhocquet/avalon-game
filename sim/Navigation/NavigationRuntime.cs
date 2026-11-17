using Meesles.Avalon.Sim.Assets;
using xpTURN.Klotho.Deterministic.Navigation;
using xpTURN.Klotho.Logging;

namespace Meesles.Avalon.Sim.Navigation;

public class NavigationRuntime {
  private NavigationRuntime(FPNavMesh navMesh, FPNavMeshQuery query, FPNavMeshPathfinder pathfinder,
    FPNavMeshFunnel funnel, FPNavAvoidance avoidance, FPNavAgentSystem agentSystem,
    FlowFieldCache flowFields) {
    NavMesh = navMesh;
    Query = query;
    Pathfinder = pathfinder;
    Funnel = funnel;
    Avoidance = avoidance;
    AgentSystem = agentSystem;
    FlowFields = flowFields;
  }

  public FPNavMesh NavMesh { get; }
  public FPNavMeshQuery Query { get; }
  public FPNavMeshPathfinder Pathfinder { get; }
  public FPNavMeshFunnel Funnel { get; }
  public FPNavAvoidance Avoidance { get; }
  public FPNavAgentSystem AgentSystem { get; }
  public FlowFieldCache FlowFields { get; }

  public static NavigationRuntime FromBytes(byte[] bytes, IKLogger logger, NavigationTuningAsset settings = null) {
    settings ??= new NavigationTuningAsset(AssetIds.NavigationTuning);
    var tuning = new FPNavTuning(collisionResolveIterations: settings.CollisionResolveIterations);
    var navMesh = FPNavMeshSerializer.Deserialize(bytes);
    var query = new FPNavMeshQuery(navMesh, logger, tuning);
    var pathfinder = new FPNavMeshPathfinder(navMesh, query, logger, tuning);
    var funnel = new FPNavMeshFunnel(navMesh, query, logger, tuning);
    // NavigationAgentSystem applies frame-specific ORCA tuning.
    var avoidance = new FPNavAvoidance(tuning);
    var agentSystem = new FPNavAgentSystem(navMesh, query, pathfinder, funnel, logger, tuning);
    agentSystem.SetAvoidance(avoidance);
    var flowFields = new FlowFieldCache(navMesh);

    return new NavigationRuntime(navMesh, query, pathfinder, funnel, avoidance, agentSystem, flowFields);
  }
}
