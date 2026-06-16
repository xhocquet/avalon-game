using xpTURN.Klotho.Core;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Logging;
using Meesles.Avalon;

namespace Meesles.Avalon.Server
{
  public class AvalonServerCallbacks : ISimulationCallbacks
  {
    private readonly IKLogger _logger;
    private readonly int _maxPlayers;

    public AvalonServerCallbacks(IKLogger logger, int maxPlayers)
    {
      _logger = logger;
      _maxPlayers = maxPlayers;
    }

    public void RegisterSystems(EcsSimulation simulation)
    {
      SimulationSetup.RegisterSystems(simulation);
    }

    public void OnInitializeWorld(IKlothoEngine engine)
    {
      SimulationSetup.InitializeWorld(engine, _maxPlayers);
    }

    public void OnPollInput(int playerId, int tick, ICommandSender sender)
    {
      // no-op: the server produces no local input. ServerInputCollector gathers the
      // client input messages and injects them into the simulation per tick.
    }
  }
}
