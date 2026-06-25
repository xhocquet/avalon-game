using Godot;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Models;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Logging;

namespace Meesles.Avalon.Client {
  public class SimCallbacks : ISimulationCallbacks {
    private InputCapture _input;
    private readonly byte[] _navMeshBytes;
    private readonly IKLogger _logger;

    public SimCallbacks(InputCapture input, byte[] navMeshBytes, IKLogger logger) {
      _input = input;
      _navMeshBytes = navMeshBytes;
      _logger = logger;
    }

    public void SetInput(InputCapture input) {
      _input = input;
    }

    public void RegisterSystems(EcsSimulation simulation) {
      SimulationSetup.RegisterSystems(simulation, NavigationRuntime.FromBytes(_navMeshBytes, _logger));
    }

    public void OnInitializeWorld(IKlothoEngine engine) {
      var frame = engine.PredictedFrame.Frame;

      if (frame.AssetRegistry.TryGet<MapLayoutAsset>(out var layout)) {
        int n = layout.MarkerTypes?.Length ?? 0;
        GD.Print($"[SimCallbacks] MapLayout has {n} markers (maxPlayers={engine.SessionConfig.MaxPlayers}):");
        for (int i = 0; i < n; i++) {
          var p = layout.MarkerPositions[i];
          GD.Print($"  [{i}] type={(MapMarkerType)layout.MarkerTypes[i]} team={layout.MarkerTeams[i]} pos=({p.x:F2}, {p.y:F2}, {p.z:F2})");
        }
      }
      else {
        GD.PrintErr("[SimCallbacks] No MapLayoutAsset in registry.");
      }

      SimulationSetup.InitializeWorld(engine, engine.SessionConfig.MaxPlayers);

      GD.Print("[SimCallbacks] Post-init entity positions:");
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
        GD.Print($"  {kind} team={team.TeamId} pos=({p.x:F2}, {p.y:F2}, {p.z:F2})");
      }
    }

    public void OnPollInput(int playerId, int tick, ICommandSender sender) {
      if (_input != null && _input.TryConsumeMoveCommand(out var command))
        sender.Send(command);
    }
  }
}
