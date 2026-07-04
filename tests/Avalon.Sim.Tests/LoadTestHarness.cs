using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Meesles.Avalon.Sim.Models;
using Xunit;
using Xunit.Abstractions;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Logging;

namespace Meesles.Avalon.Sim.Tests;

public class LoadTestHarness {
  private readonly ITestOutputHelper _output;

  public LoadTestHarness(ITestOutputHelper output) {
    _output = output;
  }

  [Theory]
  [InlineData(1_000)]
  [InlineData(5_000)]
  [InlineData(10_000)]
  public void RunLoadTest(int totalTicks) {
    const int reportInterval = 500;

    var harness = SimHarness.CreateInitialized();
    var timingLogger = new TimingCapture();
    var snapshots = new List<TickSnapshot>();
    var wallClock = Stopwatch.StartNew();

    for (int tick = 0; tick < totalTicks; tick++) {
      harness.Tick();

      bool isReportTick = (tick + 1) % reportInterval == 0 || tick == totalTicks - 1;
      if (!isReportTick)
        continue;

      double elapsedMs = wallClock.Elapsed.TotalMilliseconds;
      int entityCount = harness.Frame.Entities.Count;
      int minionCount = harness.Count<Minion>();

      harness.Simulation.LogSystemTimings(timingLogger, $"tick-{tick + 1}", KLogLevel.Information);

      snapshots.Add(new TickSnapshot {
        Tick = tick + 1,
        WallClockMs = elapsedMs,
        EntityCount = entityCount,
        MinionCount = minionCount,
        StateHash = harness.StateHash,
      });
    }

    wallClock.Stop();
    WriteReport(totalTicks, wallClock, snapshots, timingLogger.Lines);
  }

  private void WriteReport(
      int totalTicks,
      Stopwatch wallClock,
      List<TickSnapshot> snapshots,
      List<string> timingLines) {
    var sb = new StringBuilder();

    sb.AppendLine($"=== Load Test: {totalTicks} ticks ===");
    sb.AppendLine($"DeltaTime: {SimHarness.DefaultDeltaTimeMs}ms | MaxEntities: {SimHarness.DefaultMaxEntities}");
    sb.AppendLine();

    sb.AppendLine($"{"Tick",8} {"WallMs",10} {"ms/tick",8} {"Entities",10} {"Minions",10} {"Hash",20}");

    for (int i = 0; i < snapshots.Count; i++) {
      var s = snapshots[i];
      int prevTick = i == 0 ? 0 : snapshots[i - 1].Tick;
      double prevMs = i == 0 ? 0 : snapshots[i - 1].WallClockMs;
      double intervalMs = s.WallClockMs - prevMs;
      int intervalTicks = s.Tick - prevTick;
      double msPerTick = intervalTicks > 0 ? intervalMs / intervalTicks : 0;

      sb.AppendLine($"{s.Tick,8} {s.WallClockMs,10:F1} {msPerTick,8:F3} {s.EntityCount,10} {s.MinionCount,10} {s.StateHash,20}");
    }

    sb.AppendLine();
    sb.AppendLine($"Total wall-clock: {wallClock.Elapsed.TotalSeconds:F2}s for {totalTicks} ticks");
    sb.AppendLine($"Average: {wallClock.Elapsed.TotalMilliseconds / totalTicks:F3} ms/tick");
    sb.AppendLine();
    sb.AppendLine("=== Per-System Timings (avg ms, per reporting window) ===");
    foreach (string line in timingLines)
      sb.AppendLine(line);

    string report = sb.ToString();

    _output.WriteLine(report);

    string dir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "TestResults", "loadtest");
    Directory.CreateDirectory(dir);
    string filePath = Path.Combine(dir, $"loadtest_{totalTicks}.txt");
    File.WriteAllText(filePath, report);
  }

  private struct TickSnapshot {
    public int Tick;
    public double WallClockMs;
    public int EntityCount;
    public int MinionCount;
    public long StateHash;
  }

  private sealed class TimingCapture : IKLogger {
    public readonly List<string> Lines = new();

    public bool IsEnabled(KLogLevel level) => true;

    public void Log(KLogLevel level, string message, Exception exception) {
      Lines.Add(message);
    }
  }
}
