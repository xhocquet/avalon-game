using System;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Logging;

namespace Meesles.Avalon.Sim;

// Gameplay logging that fires once per tick instead of once per execution of that tick. A server-driven
// client resimulates its whole predicted window on every verified batch, so a raw frame.Logger call in a
// system or *Actions class reappears each time the tick is replayed — a dozen identical lines per event
// is normal, and interleaved tick numbers show up once two ticks are in the window.
//
// The client binds the engine's stage at session start; the server and tests leave it unbound and log
// every call. Gates output only, never sim state, so it cannot affect determinism.
public static class SimLog {
  private static Func<bool> _isResimulating;

  public static void BindStage(Func<bool> isResimulating) {
    _isResimulating = isResimulating;
  }

  public static void UnbindStage() {
    _isResimulating = null;
  }

  // Log() rather than the KInformation/KWarning extensions: those take an interpolated-string handler
  // by ref, which an already-built string argument cannot bind to.
  public static void Info(ref Frame frame, string message) {
    if (Suppressed) return;
    frame.Logger?.Log(KLogLevel.Information, message, null);
  }

  public static void Warning(ref Frame frame, string message) {
    if (Suppressed) return;
    frame.Logger?.Log(KLogLevel.Warning, message, null);
  }

  private static bool Suppressed => _isResimulating != null && _isResimulating();
}
