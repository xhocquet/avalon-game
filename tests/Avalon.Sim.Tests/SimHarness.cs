using System;
using System.Collections.Generic;
using System.IO;
using Meesles.Avalon;
using Meesles.Avalon.Sim.Navigation;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using AvalonMoveCommand = Meesles.Avalon.Sim.Commands.MoveCommand;

namespace Meesles.Avalon.Sim.Tests;

public class SimHarness {
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
    int deltaTimeMs = DefaultDeltaTimeMs,
    bool spawnHeroesNow = true) {
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
    // Headless harness has no lobby/faction-select flow, so by default spawn heroes immediately
    // with the default faction. Pass spawnHeroesNow: false to exercise the networked deferred
    // path where HeroSpawnSystem waits for a SelectFactionCommand.
    SimulationSetup.InitializeWorld(ref frame, maxPlayers, spawnHeroesNow);

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
      command.UnitIds.Add(sourceUnitId);

    return command;
  }

  public static Commands.SelectFactionCommand SelectFactionCommand(
    int playerId, int tick, int factionId) {
    return new Commands.SelectFactionCommand {
      PlayerId = playerId,
      Tick = tick,
      FactionId = factionId,
    };
  }

  public static Commands.UpgradeSkillCommand UpgradeSkillCommand(int playerId, int tick, int slot) {
    return new Commands.UpgradeSkillCommand {
      PlayerId = playerId,
      Tick = tick,
      Slot = slot,
    };
  }

  public static Commands.CastSkillCommand CastSkillCommand(int playerId, int tick, int slot) {
    return CastSkillCommand(playerId, tick, slot, FP64.Zero, FP64.Zero);
  }

  public static Commands.CastSkillCommand CastSkillCommand(
    int playerId, int tick, int slot, FP64 targetX, FP64 targetZ) {
    return new Commands.CastSkillCommand {
      PlayerId = playerId,
      Tick = tick,
      Slot = slot,
      TargetX = targetX,
      TargetZ = targetZ,
    };
  }

  public static Commands.SetCheatCommand SetCheatCommand(
    int playerId, int tick, CheatFlags flags, bool enabled = true) {
    return new Commands.SetCheatCommand {
      PlayerId = playerId,
      Tick = tick,
      Flags = (int)flags,
      Enabled = enabled ? 1 : 0,
    };
  }

  public static Commands.DebugCommand DebugCommand(
    int playerId, int tick, DebugAction action, int param = 0) {
    return DebugCommand(playerId, tick, action, param, FP64.Zero, FP64.Zero);
  }

  public static Commands.DebugCommand DebugCommand(
    int playerId, int tick, DebugAction action, int param, FP64 targetX, FP64 targetZ,
    int factionId = 0) {
    return new Commands.DebugCommand {
      PlayerId = playerId,
      Tick = tick,
      Action = (int)action,
      Param = param,
      FactionId = factionId,
      TargetX = targetX,
      TargetZ = targetZ,
    };
  }

  // ScoreSystem refuses to judge a match until the teamless prune has settled which teams hold a base,
  // which is what a real match gets from TeamPruneSystem on its first ticks.
  public void CompleteMatchSetup() {
    var frame = Frame;
    new TeamPruneSystem().Update(ref frame);
  }

  public EntityRef FindHero(int playerId) {
    var frame = Frame;
    var filter = frame.Filter<Components.Hero>();
    while (filter.Next(out var entity)) {
      if (frame.GetReadOnly<Components.Hero>(entity).PlayerId == playerId)
        return entity;
    }

    throw new InvalidOperationException($"No hero for player {playerId}.");
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
