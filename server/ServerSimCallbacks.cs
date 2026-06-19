using xpTURN.Klotho.Core;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Logging;

namespace Meesles.Avalon.Server {
  public class ServerSimCallbacks : ISimulationCallbacks {
    private readonly IKLogger _logger;
    private readonly int _maxPlayers;

    public ServerSimCallbacks(IKLogger logger, int maxPlayers) {
      _logger = logger;
      _maxPlayers = maxPlayers;
    }

    public void RegisterSystems(EcsSimulation simulation) {
      SimulationSetup.RegisterSystems(simulation);
    }

    public void OnInitializeWorld(IKlothoEngine engine) {
      SimulationSetup.InitializeWorld(engine, _maxPlayers);
    }

    public void OnPollInput(int playerId, int tick, ICommandSender sender) {
      // no-op: the server produces no local input. ServerInputCollector gathers the
      // client input messages and injects them into the simulation per tick.
    }
  }
}
