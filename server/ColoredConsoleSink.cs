using System;
using System.Runtime.InteropServices;
using xpTURN.Klotho.Logging;

namespace Meesles.Avalon.Server {
  internal sealed class ColoredConsoleSink : IKLogSink {
    private const string Teal = "\u001b[38;2;0;128;128m";
    private const string ResetForeground = "\u001b[39m";

    private readonly ConsoleSink _console = new();
    private readonly bool _useColor = EnableColor();

    private static bool EnableColor() {
      if (Console.IsOutputRedirected
        || Environment.GetEnvironmentVariable("NO_COLOR") != null
        || Environment.GetEnvironmentVariable("TERM") == "dumb")
        return false;

      if (!OperatingSystem.IsWindows()) return true;

      const int standardOutputHandle = -11;
      const uint enableVirtualTerminalProcessing = 0x0004;
      var handle = GetStdHandle(standardOutputHandle);
      return GetConsoleMode(handle, out uint mode)
        && SetConsoleMode(handle, mode | enableVirtualTerminalProcessing);
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetStdHandle(int standardHandle);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetConsoleMode(IntPtr consoleHandle, out uint mode);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetConsoleMode(IntPtr consoleHandle, uint mode);

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
