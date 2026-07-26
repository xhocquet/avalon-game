using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Meesles.Avalon;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Components;
using Meesles.Avalon.Sim.Navigation;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Logging;

int totalTicks = args.Length > 0 && int.TryParse(args[0], out var t) ? t : 10_000;
const int reportInterval = 500;
const int maxPlayers = 2;
const int maxEntities = 1024;
const int maxRollbackTicks = 50;
const int deltaTimeMs = 16;

WarmupRegistry.RunAll();

var assetRegistry = LoadAssetRegistry();
var navigation = LoadNavigationRuntime();
var simulation = new EcsSimulation(maxEntities, maxRollbackTicks, deltaTimeMs, assetRegistry: assetRegistry);
SimulationSetup.RegisterSystems(simulation, navigation);
simulation.Initialize();

var frame = simulation.Frame;
SimulationSetup.InitializeWorld(ref frame, maxPlayers);

var timingLogger = new TimingCapture();
var wallClock = Stopwatch.StartNew();

Console.WriteLine($"Running {totalTicks} ticks...");

double prevChunkMs = 0;
int prevChunkTick = 0;

for (int tick = 0; tick < totalTicks; tick++) {
    simulation.Tick(new List<ICommand>());

    if ((tick + 1) % 100 == 0) {
        double nowMs = wallClock.Elapsed.TotalMilliseconds;
        double chunkMs = nowMs - prevChunkMs;
        double msPerTick = chunkMs / (tick + 1 - prevChunkTick);
        Console.WriteLine($"  [{tick + 1,5}/{totalTicks}] {nowMs,8:F0}ms  ({msPerTick:F2} ms/tick)");
        prevChunkMs = nowMs;
        prevChunkTick = tick + 1;
    }

    bool isReportTick = (tick + 1) % reportInterval == 0 || tick == totalTicks - 1;
    if (!isReportTick) continue;

    double elapsedMs = wallClock.Elapsed.TotalMilliseconds;
    int entityCount = simulation.Frame.Entities.Count;
    int minionCount = CountComponents<Minion>(simulation.Frame);

    simulation.LogSystemTimings(timingLogger, $"tick-{tick + 1}", KLogLevel.Information);

    Console.WriteLine($"  tick {tick + 1,6}  {elapsedMs,10:F1}ms  entities={entityCount}  minions={minionCount}  hash={simulation.GetStateHash()}");
}

wallClock.Stop();
Console.WriteLine($"\nTotal: {wallClock.Elapsed.TotalSeconds:F2}s for {totalTicks} ticks ({wallClock.Elapsed.TotalMilliseconds / totalTicks:F3} ms/tick)");
Console.WriteLine("\nPer-system timings:");
foreach (string line in timingLogger.Lines)
    Console.WriteLine($"  {line}");

static int CountComponents<TComponent>(Frame frame) where TComponent : unmanaged, IComponent {
    int count = 0;
    var filter = frame.Filter<TComponent>();
    while (filter.Next(out _)) count++;
    return count;
}

static IDataAssetRegistry LoadAssetRegistry() {
    string assetPath = Path.Combine(AppContext.BaseDirectory, "Data", "Assets.bytes");
    string layoutPath = Path.Combine(AppContext.BaseDirectory, "Data", "MapLayout.bytes");
    var assets = DataAssetReader.LoadMixedCollectionFromBytes(assetPath);
    var layoutAssets = DataAssetReader.LoadMixedCollectionFromBytes(layoutPath);
    IDataAssetRegistryBuilder builder = new DataAssetRegistry();
    builder.RegisterRange(assets);
    builder.RegisterRange(layoutAssets);
    return builder.Build();
}

static NavigationRuntime LoadNavigationRuntime() {
    string navPath = Path.Combine(AppContext.BaseDirectory, "Data", "NavigationRegion3D.NavMeshData.bytes");
    return NavigationRuntime.FromBytes(File.ReadAllBytes(navPath), logger: null);
}

sealed class TimingCapture : IKLogger {
    public readonly List<string> Lines = new();
    public bool IsEnabled(KLogLevel level) => true;
    public void Log(KLogLevel level, string message, Exception exception) => Lines.Add(message);
}
