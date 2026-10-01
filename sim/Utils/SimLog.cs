using System;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Logging;

namespace Meesles.Avalon.Sim;

// Gameplay logging that fires once per tick, preventing duplicate logs during rollback/replay code
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

  public static void Debug(ref Frame frame, string message) {
    if (Suppressed) return;
    frame.Logger?.Log(KLogLevel.Debug, message, null);
  }

  private static bool Suppressed => _isResimulating != null && _isResimulating();
}
