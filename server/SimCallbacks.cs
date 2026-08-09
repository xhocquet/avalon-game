using System;
using System.Collections.Generic;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Logging;
using xpTURN.Klotho.Network;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using Meesles.Avalon.Sim.Navigation;

namespace Meesles.Avalon.Server {
  public class SimCallbacks : ISimulationCallbacks {
    private readonly IKLogger _logger;
    private readonly int _maxPlayers;
    private readonly byte[] _navMeshBytes;
    private readonly Func<RoomManager> _roomManager;
    private MatchResultSaveSystem _resultSaver;

    // roomManager is resolved lazily: RoomManager owns the factory that builds this instance, so it
    // does not exist yet at construction.
    public SimCallbacks(IKLogger logger, int maxPlayers, byte[] navMeshBytes,
      Func<RoomManager> roomManager = null) {
      _logger = logger;
      _maxPlayers = maxPlayers;
      _navMeshBytes = navMeshBytes;
      _roomManager = roomManager;
    }

    public void RegisterSystems(EcsSimulation simulation) {
      SimulationSetup.RegisterSystems(simulation, NavigationRuntime.FromBytes(_navMeshBytes, _logger));
      _resultSaver = new MatchResultSaveSystem(_logger, ResolveRoster);
      simulation.AddSystem(_resultSaver, SystemPhase.LateUpdate);
    }

    // The room that owns this callbacks instance is the one whose roster describes this match.
    private IReadOnlyList<IPlayerInfo> ResolveRoster() {
      var manager = _roomManager?.Invoke();
      if (manager == null)
        return null;

      for (var roomId = 0; roomId < manager.MaxRooms; roomId++) {
        var room = manager.GetRoom(roomId);
        if (ReferenceEquals(room?.Callbacks, this))
          return room.NetworkService.Players;
      }

      return null;
    }

    public void OnInitializeWorld(IKlothoEngine engine) {
      _resultSaver?.SetSessionParameters(engine.RandomSeed, engine.SessionConfig.MaxPlayers,
        engine.SessionConfig.MinPlayers);

      var frame = engine.PredictedFrame.Frame;

      if (frame.AssetRegistry.TryGet<MapLayoutAsset>(out var layout)) {
        int n = layout.MarkerTypes?.Length ?? 0;
        _logger.KInformation($"[SimCallbacks] MapLayout has {n} markers (maxPlayers={_maxPlayers}):");
        for (int i = 0; i < n; i++) {
          var p = layout.MarkerPositions[i];
          _logger.KInformation(
            $"  [{i}] type={(MapMarkerType)layout.MarkerTypes[i]} team={layout.MarkerTeams[i]} pos=({p.x:F2}, {p.y:F2}, {p.z:F2})");
        }
      }
      else {
        _logger.KWarning($"[SimCallbacks] No MapLayoutAsset in registry.");
      }

      SimulationSetup.InitializeWorld(engine, _maxPlayers);

      _logger.KInformation($"[SimCallbacks] Post-init entity positions:");
      var postFrame = engine.PredictedFrame.Frame;
      var filter = postFrame.Filter<TransformComponent, TeamComponent>();
      while (filter.Next(out var entity)) {
        ref readonly var pos = ref postFrame.GetReadOnly<TransformComponent>(entity);
        ref readonly var team = ref postFrame.GetReadOnly<TeamComponent>(entity);
        string kind = postFrame.Has<Crystal>(entity) ? "Crystal"
          : postFrame.Has<Turret>(entity) ? "Turret"
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
