using System;
using Godot;
using xpTURN.Klotho.Godot;
using xpTURN.Klotho.Logging;

namespace Meesles.Avalon;

public sealed class ClientLogSink : IKLogSink {
  private readonly GodotLogSink _godot = new();

  public void Write(KLogLevel level, string message, Exception exception) {
    if (level == KLogLevel.Warning && exception == null &&
        (message.StartsWith("[KlothoEngine] Tick gap:", StringComparison.Ordinal) ||
         message.StartsWith("[KlothoEngine] ClientTick: Accumulator clamped:", StringComparison.Ordinal))) {
      GD.Print($"WARNING: {message}");
      return;
    }
    _godot.Write(level, message, exception);
  }

  public void Flush() => _godot.Flush();
  public void Dispose() => _godot.Dispose();
}
