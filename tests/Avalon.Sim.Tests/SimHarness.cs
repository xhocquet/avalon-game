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
  public Frame Frame => Simulation.Frame;
  public long StateHash => Simulation.GetStateHash();

  private SimHarness(EcsSimulation simulation, IDataAssetRegistry assetRegistry) {
    Simulation = simulation;
    AssetRegistry = assetRegistry;
  }

  public static SimHarness CreateInitialized(
      int maxPlayers = DefaultMaxPlayers,
      int maxEntities = DefaultMaxEntities,
      int maxRollbackTicks = DefaultMaxRollbackTicks,
      int deltaTimeMs = DefaultDeltaTimeMs) {
    WarmupRegistry.RunAll();

    var assetRegistry = LoadAssetRegistry();
    var simulation = new EcsSimulation(
        maxEntities,
        maxRollbackTicks,
        deltaTimeMs,
        assetRegistry: assetRegistry);

    SimulationSetup.RegisterSystems(simulation);
    simulation.Initialize();

    var frame = simulation.Frame;
    SimulationSetup.InitializeWorld(ref frame, maxPlayers);

    return new SimHarness(simulation, assetRegistry);
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
}
