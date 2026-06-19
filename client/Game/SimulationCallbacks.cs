using xpTURN.Klotho.Core;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon {
  public class SimulationCallbacks : ISimulationCallbacks {
    private readonly InputCapture _input;

    public SimulationCallbacks(InputCapture input) {
      _input = input;
    }

    public void RegisterSystems(EcsSimulation simulation) {
      SimulationSetup.RegisterSystems(simulation);
    }

    public void OnInitializeWorld(IKlothoEngine engine) {
      SimulationSetup.InitializeWorld(engine, engine.SessionConfig.MaxPlayers);
    }

    public void OnPollInput(int playerId, int tick, ICommandSender sender) {
      if (_input.TryConsumeMoveCommand(out var command))
        sender.Send(command);
    }
  }
}
