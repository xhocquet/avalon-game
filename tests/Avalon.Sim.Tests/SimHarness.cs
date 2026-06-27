using System;
using System.Collections.Generic;
using System.IO;
using Meesles.Avalon;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using AvalonMoveCommand = Meesles.Avalon.Sim.Commands.MoveCommand;

namespace Meesles.Avalon.Sim.Tests;

public sealed class SimHarness {
  public const int DefaultMaxPlayers = 2;
  public const int DefaultMaxEntities = 1024;
  public const int DefaultMaxRollbackTicks = 50;
  public const int DefaultDeltaTimeMs = 16;

  public EcsSimulation Simulation { get; }
  public IDataAssetRegistry AssetRegistry { get; }
  public NavigationRuntime Navigation { get; }
  public Frame Frame => Simulation.Frame;
  public long StateHash => Simulation.GetStateHash();

  private SimHarness(EcsSimulation simulation, IDataAssetRegistry assetRegistry, NavigationRuntime navigation) {
    Simulation = simulation;
    AssetRegistry = assetRegistry;
    Navigation = navigation;
  }

  public static SimHarness CreateInitialized(
      int maxPlayers = DefaultMaxPlayers,
      int maxEntities = DefaultMaxEntities,
      int maxRollbackTicks = DefaultMaxRollbackTicks,
      int deltaTimeMs = DefaultDeltaTimeMs) {
    WarmupRegistry.RunAll();

    var assetRegistry = LoadAssetRegistry();
    var navigation = LoadNavigationRuntime();
    var simulation = new EcsSimulation(
        maxEntities,
        maxRollbackTicks,
        deltaTimeMs,
        assetRegistry: assetRegistry);

    SimulationSetup.RegisterSystems(simulation, navigation);
    simulation.Initialize();

    var frame = simulation.Frame;
    SimulationSetup.InitializeWorld(ref frame, maxPlayers);

    return new SimHarness(simulation, assetRegistry, navigation);
  }

  public void Tick(params ICommand[] commands) {
    Simulation.Tick(new List<ICommand>(commands));
  }

  public static AvalonMoveCommand MoveCommand(int playerId, int tick, FP64 targetX, FP64 targetZ) {
    return new AvalonMoveCommand {
      PlayerId = playerId,
      Tick = tick,
      TargetX = targetX,
      TargetZ = targetZ,
    };
  }

  public static Meesles.Avalon.Sim.Commands.AttackCommand AttackCommand(
      int playerId,
      int tick,
      int targetUnitId,
      params int[] sourceUnitIds) {
    var command = new Meesles.Avalon.Sim.Commands.AttackCommand {
      PlayerId = playerId,
      Tick = tick,
      TargetUnitId = targetUnitId,
    };

    foreach (int sourceUnitId in sourceUnitIds)
      command.AddSourceUnitId(sourceUnitId);

    return command;
  }

  public int Count<TComponent>() where TComponent : unmanaged, IComponent {
    int count = 0;
    var filter = Frame.Filter<TComponent>();
    while (filter.Next(out _))
      count++;

    return count;
  }

  private static IDataAssetRegistry LoadAssetRegistry() {
    string assetPath = Path.Combine(AppContext.BaseDirectory, "Data", "Assets.bytes");
    if (!File.Exists(assetPath))
      throw new FileNotFoundException("Shared sim data asset was not copied to the test output.", assetPath);

    string layoutPath = Path.Combine(AppContext.BaseDirectory, "Data", "MapLayout.bytes");
    if (!File.Exists(layoutPath))
      throw new FileNotFoundException("Shared map layout asset was not copied to the test output.", layoutPath);

    var assets = DataAssetReader.LoadMixedCollectionFromBytes(assetPath);
    var layoutAssets = DataAssetReader.LoadMixedCollectionFromBytes(layoutPath);
    IDataAssetRegistryBuilder builder = new DataAssetRegistry();
    builder.RegisterRange(assets);
    builder.RegisterRange(layoutAssets);
    return builder.Build();
  }

  private static NavigationRuntime LoadNavigationRuntime() {
    string navPath = Path.Combine(AppContext.BaseDirectory, "Data", "NavigationRegion3D.NavMeshData.bytes");
    if (!File.Exists(navPath))
      throw new FileNotFoundException("Shared navigation mesh was not copied to the test output.", navPath);

    return NavigationRuntime.FromBytes(File.ReadAllBytes(navPath), logger: null);
  }
}
