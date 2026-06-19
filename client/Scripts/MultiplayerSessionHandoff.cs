using xpTURN.Klotho.Core;
using xpTURN.Klotho.Godot;
using xpTURN.Klotho.LiteNetLib;
using xpTURN.Klotho.Logging;

namespace Meesles.Avalon {
  public sealed class MultiplayerSessionHandoff {
    private static MultiplayerSessionHandoff _pending;

    public IKLogger Logger { get; init; }
    public LiteNetLibTransport Transport { get; init; }
    public KlothoSessionFlow Flow { get; init; }
    public KlothoSession Session { get; init; }
    public SimulationCallbacks SimulationCallbacks { get; init; }
    public ViewCallbacks ViewCallbacks { get; init; }
    public GodotSessionDriver Driver { get; init; }
    public ISimulationConfig SimulationConfig { get; init; }
    public ISessionConfig SessionConfig { get; init; }

    public static bool HasPending => _pending != null;

    public static void Store(MultiplayerSessionHandoff handoff) {
      _pending = handoff;
    }

    public static MultiplayerSessionHandoff Consume() {
      var handoff = _pending;
      _pending = null;
      return handoff;
    }
  }
}
