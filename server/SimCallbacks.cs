using xpTURN.Klotho.Core;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Logging;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Models;

namespace Meesles.Avalon.Server {
  public class SimCallbacks : ISimulationCallbacks {
    private readonly IKLogger _logger;
    private readonly int _maxPlayers;
    private readonly byte[] _navMeshBytes;

    public SimCallbacks(IKLogger logger, int maxPlayers, byte[] navMeshBytes) {
      _logger = logger;
      _maxPlayers = maxPlayers;
      _navMeshBytes = navMeshBytes;
    }

    public void RegisterSystems(EcsSimulation simulation) {
      SimulationSetup.RegisterSystems(simulation, NavigationRuntime.FromBytes(_navMeshBytes, _logger));
    }

    public void OnInitializeWorld(IKlothoEngine engine) {
      var frame = engine.PredictedFrame.Frame;

      if (frame.AssetRegistry.TryGet<MapLayoutAsset>(out var layout)) {
        int n = layout.MarkerTypes?.Length ?? 0;
        _logger.KInformation($"[SimCallbacks] MapLayout has {n} markers (maxPlayers={_maxPlayers}):");
        for (int i = 0; i < n; i++) {
          var p = layout.MarkerPositions[i];
          _logger.KInformation($"  [{i}] type={(MapMarkerType)layout.MarkerTypes[i]} team={layout.MarkerTeams[i]} pos=({p.x:F2}, {p.y:F2}, {p.z:F2})");
        }
      }
      else {
        _logger.KWarning($"[SimCallbacks] No MapLayoutAsset in registry.");
      }

      SimulationSetup.InitializeWorld(engine, _maxPlayers);

      _logger.KInformation($"[SimCallbacks] Post-init entity positions:");
      var postFrame = engine.PredictedFrame.Frame;
      var filter = postFrame.Filter<TransformComponent, Team>();
      while (filter.Next(out var entity)) {
        ref readonly var pos = ref postFrame.GetReadOnly<TransformComponent>(entity);
        ref readonly var team = ref postFrame.GetReadOnly<Team>(entity);
        string kind = postFrame.Has<Base>(entity) ? "Base"
          : postFrame.Has<SpawnPoint>(entity) ? "SpawnPoint"
          : postFrame.Has<Hero>(entity) ? "Hero"
          : "Unknown";
        var p = pos.Position;
        _logger.KInformation($"  {kind} team={team.TeamId} pos=({p.x:F2}, {p.y:F2}, {p.z:F2})");
      }
    }

    public void OnPollInput(int playerId, int tick, ICommandSender sender) {
      // no-op: the server produces no local input. ServerInputCollector gathers the
      // client input messages and injects them into the simulation per tick.
    }
  }
}
