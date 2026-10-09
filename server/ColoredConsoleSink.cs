using System;
using xpTURN.Klotho.Logging;

namespace Meesles.Avalon.Server {
  internal sealed class ColoredConsoleSink : IKLogSink {
    private const string Teal = "\u001b[38;2;0;128;128m";
    private const string ResetForeground = "\u001b[39m";

    private readonly ConsoleSink _console = new();
    private readonly bool _useColor = !Console.IsOutputRedirected
      && Environment.GetEnvironmentVariable("NO_COLOR") == null
      && Environment.GetEnvironmentVariable("TERM") != "dumb";

    public void Write(KLogLevel level, string message, Exception exception) {
      if (_useColor && !string.IsNullOrEmpty(message) && message[0] == '[') {
        int end = message.IndexOf(']');
        if (end > 0) {
          message = string.Concat(Teal.AsSpan(), message.AsSpan(0, end + 1),
            ResetForeground.AsSpan(), message.AsSpan(end + 1));
        }
      }

      _console.Write(level, message, exception);
    }

    public void Flush() => _console.Flush();
    public void Dispose() => _console.Dispose();
  }
}
