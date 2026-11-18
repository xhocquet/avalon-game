using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using Meesles.Avalon.Sim.Factories;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Deterministic.Navigation;
using xpTURN.Klotho.ECS;
using Xunit;

namespace Meesles.Avalon.Sim.Tests;

public class SimulationCapacityTests {
  [Fact]
  public void FullRosterSpawnsWithinHeroCapsAndSurvivesRollback() {
    var harness = SimHarness.CreateInitialized(maxPlayers: SimulationCapacity.PlayerSlots, spawnHeroesNow: false);
    var frame = harness.Frame;
    var selections = frame.Filter<PlayerFaction>();
    while (selections.Next(out var slot))
      frame.Get<PlayerFaction>(slot).Confirmed = 1;
    harness.Tick();

    int[] capped = [ComponentIds.Player, ComponentIds.Hero, ComponentIds.PlayerFaction,
      ComponentIds.Inventory, ComponentIds.Experience, ComponentIds.Respawns, ComponentIds.Skills, ComponentIds.Resources];
    foreach (var id in capped) {
      frame.GetLiveCount(id).Should().Be(SimulationCapacity.PlayerSlots);
      ComponentStorageRegistry.GetLayout(id).SlotCapacity.Should().Be(SimulationCapacity.PlayerSlots);
    }

    UnitLookup.TryGetPlayerHero(ref frame, 1, out var hero).Should().BeTrue();
    var gold = frame.GetReadOnly<Inventory>(hero).Gold;
    var tick = frame.Tick;
    harness.Simulation.SaveSnapshot();
    frame.Get<Inventory>(hero).Gold = gold + 123;
    harness.Tick();
    harness.Simulation.Rollback(tick);
    harness.Frame.GetReadOnly<Inventory>(hero).Gold.Should().Be(gold);
    foreach (var id in capped)
      harness.Frame.GetLiveCount(id).Should().Be(SimulationCapacity.PlayerSlots);
  }

  [Fact]
  public void HeroCapsDoNotLimitMinionsOrSharedComponents() {
    var harness = SimHarness.CreateInitialized(maxPlayers: SimulationCapacity.PlayerSlots);
    var frame = harness.Frame;
    var stats = frame.AssetRegistry.Get<MinionStatsAsset>();
    for (var i = 0; i < SimulationCapacity.PlayerSlots * 3; i++)
      MinionFactory.Spawn(ref frame, stats, FPVector3.Zero, FP64.Zero, 1, 0);
    frame.GetLiveCount(ComponentIds.Minion).Should().Be(SimulationCapacity.PlayerSlots * 3);
    ComponentStorageRegistry.GetLayout(ComponentIds.Health).SlotCapacity.Should().Be(frame.MaxEntities);
    ComponentStorageRegistry.GetLayout(ComponentIds.Stats).SlotCapacity.Should().Be(frame.MaxEntities);
    ComponentStorageRegistry.GetLayout(ComponentStorageRegistry.GetTypeId<NavAgentComponent>())
      .SlotCapacity.Should().Be(frame.MaxEntities);
  }

  [Fact]
  public void HeroStorageRejectsOverflowAndReusesFreedSlots() {
    var harness = SimHarness.CreateInitialized(maxPlayers: SimulationCapacity.PlayerSlots);
    var frame = harness.Frame;
    var extra = frame.CreateEntity();
    Action overflow = () => frame.Add(extra, new Inventory());
    overflow.Should().Throw<InvalidOperationException>();
    UnitLookup.TryGetPlayerHero(ref frame, 1, out var hero).Should().BeTrue();
    frame.Remove<Inventory>(hero);
    frame.Add(extra, new Inventory());
    frame.GetLiveCount(ComponentIds.Inventory).Should().Be(SimulationCapacity.PlayerSlots);
  }

  [Fact]
  public void OasisCapsSupportEjectionLandingAndRollback() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var rules = frame.AssetRegistry.Get<PickupRulesAsset>();
    rules.OasisPrepareDurationMs = frame.DeltaTimeMs * 3;
    rules.OasisFlightDurationMs = frame.DeltaTimeMs * 3;
    var oases = frame.Filter<Oasis>();
    oases.Next(out var oasis).Should().BeTrue();
    frame.Get<Oasis>(oasis).SpawnCooldownRemainingMs = 0;
    new OasisSpawnSystem().Update(ref frame);
    frame.GetLiveCount(ComponentIds.Oasis).Should().Be(SimulationCapacity.OasisSlots);
    frame.Has<OasisEjectPending>(oasis).Should().BeTrue();
    var pendingMs = frame.GetReadOnly<OasisEjectPending>(oasis).RemainingMs;
    var pickups = frame.GetLiveCount(ComponentIds.Pickup);
    int[] capped = [ComponentIds.Oasis, ComponentIds.OasisEjectPending, ComponentIds.OasisResourceLanding];
    foreach (var id in capped)
      ComponentStorageRegistry.GetLayout(id).SlotCapacity.Should().Be(SimulationCapacity.OasisSlots);

    var snapshotTick = frame.Tick;
    harness.Simulation.SaveSnapshot();
    for (var i = 0; i < 6; i++)
      harness.Tick();
    frame.GetLiveCount(ComponentIds.Pickup).Should().Be(pickups + 1);
    frame.Has<OasisEjectPending>(oasis).Should().BeFalse();
    frame.Has<OasisResourceLanding>(oasis).Should().BeFalse();
    var hash = harness.StateHash;

    harness.Simulation.Rollback(snapshotTick);
    frame.GetReadOnly<OasisEjectPending>(oasis).RemainingMs.Should().Be(pendingMs);
    frame.GetLiveCount(ComponentIds.Pickup).Should().Be(pickups);
    for (var i = 0; i < 6; i++)
      harness.Tick();
    harness.StateHash.Should().Be(hash);
  }

  [Fact]
  public void ExtraOasisMarkersAreRejectedBeforeWorldStateChanges() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var layout = frame.AssetRegistry.Get<MapLayoutAsset>();
    var count = SimulationCapacity.OasisSlots + 1;
    layout.MarkerTypes = new int[count];
    Array.Fill(layout.MarkerTypes, (int)MapMarkerType.Oasis);
    layout.MarkerTeams = new int[count];
    layout.MarkerPositions = new FPVector3[count];
    var hash = harness.StateHash;
    Action initialize = () => SimulationSetup.InitializeWorld(ref frame, SimulationCapacity.PlayerSlots);
    initialize.Should().Throw<InvalidOperationException>()
      .WithMessage($"*oasis markers*capacity is {SimulationCapacity.OasisSlots}*");
    harness.StateHash.Should().Be(hash);
  }

  [Theory]
  [InlineData(0)]
  [InlineData(SimulationCapacity.OasisSlots)]
  public void MapsMayHaveZeroOrOneOasis(int oasisCount) {
    var layout = new MapLayoutAsset(AssetIds.MapLayout) {
      MarkerTypes = new int[oasisCount],
      MarkerTeams = new int[oasisCount],
      MarkerPositions = new FPVector3[oasisCount]
    };
    Array.Fill(layout.MarkerTypes, (int)MapMarkerType.Oasis);
    Action validate = () => SimulationCapacity.ValidateMapLayout(layout);
    validate.Should().NotThrow();
  }

  [Theory]
  [InlineData("MapLayout.bytes")]
  [InlineData("MapLayout_CombatArena.bytes")]
  [InlineData("MapLayout_NavPlayground.bytes")]
  public void AuthoredMapsRespectSharedCapacityLimits(string filename) {
    WarmupRegistry.RunAll();
    var assets = DataAssetReader.LoadMixedCollectionFromBytes(Path.Combine(AppContext.BaseDirectory, "Data", filename));
    var layout = assets.OfType<MapLayoutAsset>().Single();
    Action validate = () => SimulationCapacity.ValidateMapLayout(layout);
    validate.Should().NotThrow();
  }

  [Fact]
  public void FullStructureCapacitySpawnsAndSurvivesRollback() {
    var harness = SimHarness.CreateInitialized();
    var layout = harness.AssetRegistry.Get<MapLayoutAsset>();
    var markers = Enumerable.Range(0, layout.MarkerCount)
      .Where(i => layout.MarkerTypes[i] != (int)MapMarkerType.Turret)
      .Select(i => (Type: layout.MarkerTypes[i], Team: layout.MarkerTeams[i], Position: layout.MarkerPositions[i],
        Value: layout.MarkerValues != null && i < layout.MarkerValues.Length ? layout.MarkerValues[i] : 0)).ToList();
    for (var team = 1; team <= SimulationCapacity.PlayerSlots; team++)
      for (var i = 0; i < SimulationCapacity.TurretsPerPlayer; i++)
        markers.Add(((int)MapMarkerType.Turret, team, FPVector3.Zero, 0));
    layout.MarkerTypes = markers.Select(marker => marker.Type).ToArray();
    layout.MarkerTeams = markers.Select(marker => marker.Team).ToArray();
    layout.MarkerPositions = markers.Select(marker => marker.Position).ToArray();
    layout.MarkerValues = markers.Select(marker => marker.Value).ToArray();

    var simulation = new EcsSimulation(SimHarness.DefaultMaxEntities, maxRollbackTicks: 2,
      assetRegistry: harness.AssetRegistry);
    var frame = simulation.Frame;
    SimulationSetup.InitializeWorld(ref frame, SimulationCapacity.PlayerSlots, spawnHeroesNow: true);
    frame.GetLiveCount(ComponentIds.Turret).Should().Be(SimulationCapacity.TurretSlots);
    frame.GetLiveCount(ComponentIds.Crystal).Should().Be(SimulationCapacity.CrystalSlots);
    frame.GetLiveCount(ComponentIds.SpawnPoint).Should().Be(SimulationCapacity.SpawnPointSlots);
    ComponentStorageRegistry.GetLayout(ComponentIds.Turret).SlotCapacity.Should().Be(SimulationCapacity.TurretSlots);
    ComponentStorageRegistry.GetLayout(ComponentIds.Crystal).SlotCapacity.Should().Be(SimulationCapacity.CrystalSlots);
    ComponentStorageRegistry.GetLayout(ComponentIds.SpawnPoint).SlotCapacity.Should().Be(SimulationCapacity.SpawnPointSlots);

    var extra = frame.CreateEntity();
    Action extraTurret = () => frame.Add(extra, new Turret());
    Action extraCrystal = () => frame.Add(extra, new Crystal());
    Action extraSpawnPoint = () => frame.Add(extra, new SpawnPoint());
    extraTurret.Should().Throw<InvalidOperationException>();
    extraCrystal.Should().Throw<InvalidOperationException>();
    extraSpawnPoint.Should().Throw<InvalidOperationException>();
    simulation.SaveSnapshot();
    var hash = simulation.GetStateHash();
    var turrets = frame.Filter<Turret>();
    turrets.Next(out var turret).Should().BeTrue();
    frame.DestroyEntity(turret);
    var crystals = frame.Filter<Crystal>();
    crystals.Next(out var crystal).Should().BeTrue();
    frame.DestroyEntity(crystal);
    simulation.Rollback(frame.Tick);
    simulation.GetStateHash().Should().Be(hash);
  }

  [Theory]
  [InlineData(MapMarkerType.Turret, SimulationCapacity.TurretsPerPlayer + 1)]
  [InlineData(MapMarkerType.Crystal, SimulationCapacity.CrystalsPerPlayer + 1)]
  [InlineData(MapMarkerType.SpawnPoint, SimulationCapacity.SpawnPointsPerPlayer + 1)]
  public void ExcessStructuresForOneTeamAreRejectedBeforeWorldStateChanges(MapMarkerType type, int count) {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var layout = frame.AssetRegistry.Get<MapLayoutAsset>();
    layout.MarkerTypes = Enumerable.Repeat((int)type, count).ToArray();
    layout.MarkerTeams = Enumerable.Repeat(1, count).ToArray();
    layout.MarkerPositions = new FPVector3[count];
    var hash = harness.StateHash;
    Action initialize = () => SimulationSetup.InitializeWorld(ref frame, SimulationCapacity.PlayerSlots);
    initialize.Should().Throw<InvalidOperationException>().WithMessage("*per-player capacity*");
    harness.StateHash.Should().Be(hash);
  }

  [Theory]
  [InlineData(MapMarkerType.Turret, SimulationCapacity.TurretsPerPlayer, SimulationCapacity.TurretSlots)]
  [InlineData(MapMarkerType.Crystal, SimulationCapacity.CrystalsPerPlayer, SimulationCapacity.CrystalSlots)]
  [InlineData(MapMarkerType.SpawnPoint, SimulationCapacity.SpawnPointsPerPlayer, SimulationCapacity.SpawnPointSlots)]
  public void StructureTotalsCannotExceedSupportedRoster(MapMarkerType type, int perPlayer, int capacity) {
    var count = perPlayer * (SimulationCapacity.PlayerSlots + 1);
    var layout = new MapLayoutAsset(AssetIds.MapLayout) {
      MarkerTypes = Enumerable.Repeat((int)type, count).ToArray(),
      MarkerTeams = Enumerable.Range(0, count).Select(i => i / perPlayer + 1).ToArray(),
      MarkerPositions = new FPVector3[count]
    };
    Action validate = () => SimulationCapacity.ValidateMapLayout(layout);
    validate.Should().Throw<InvalidOperationException>().WithMessage($"*capacity is {capacity}.*");
  }

  [Fact]
  public void PickupCapacityStopsOasisSpawningAndReusesCollectedSlots() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var rules = frame.AssetRegistry.Get<PickupRulesAsset>();
    rules.MaxGroundPickups = SimulationCapacity.PickupSlots + 1;
    rules.OasisPrepareDurationMs = 0;
    rules.OasisFlightDurationMs = 0;
    ComponentStorageRegistry.GetLayout(ComponentIds.Pickup).SlotCapacity.Should().Be(SimulationCapacity.PickupSlots);
    while (frame.GetLiveCount(ComponentIds.Pickup) < SimulationCapacity.PickupSlots)
      frame.Add(frame.CreateEntity(), new Pickup());
    var extra = frame.CreateEntity();
    Action overflow = () => frame.Add(extra, new Pickup());
    overflow.Should().Throw<InvalidOperationException>();
    var oases = frame.Filter<Oasis>();
    oases.Next(out var oasis).Should().BeTrue();
    frame.Get<Oasis>(oasis).SpawnCooldownRemainingMs = 0;
    var system = new OasisSpawnSystem();
    system.Update(ref frame);
    frame.Has<OasisEjectPending>(oasis).Should().BeFalse();
    frame.GetLiveCount(ComponentIds.Pickup).Should().Be(SimulationCapacity.PickupSlots);

    var pickups = frame.Filter<Pickup>();
    pickups.Next(out var pickup).Should().BeTrue();
    frame.DestroyEntity(pickup);
    frame.Get<Oasis>(oasis).SpawnCooldownRemainingMs = 0;
    system.Update(ref frame);
    frame.GetLiveCount(ComponentIds.Pickup).Should().Be(SimulationCapacity.PickupSlots);
  }

  [Fact]
  public void ExcessPickupMarkersAreRejectedBeforeWorldStateChanges() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var layout = frame.AssetRegistry.Get<MapLayoutAsset>();
    var count = SimulationCapacity.PickupSlots + 1;
    layout.MarkerTypes = Enumerable.Repeat((int)MapMarkerType.Pickup, count).ToArray();
    layout.MarkerTeams = new int[count];
    layout.MarkerPositions = new FPVector3[count];
    var hash = harness.StateHash;
    Action initialize = () => SimulationSetup.InitializeWorld(ref frame, SimulationCapacity.PlayerSlots);
    initialize.Should().Throw<InvalidOperationException>().WithMessage("*pickup markers*capacity is 128*");
    harness.StateHash.Should().Be(hash);
  }

  [Theory]
  [InlineData(0)]
  [InlineData(SimulationCapacity.PlayerSlots + 1)]
  public void InvalidRosterCapacityIsRejected(int maxPlayers) {
    Action validate = () => SimulationCapacity.ValidatePlayerCount(maxPlayers);
    validate.Should().Throw<ArgumentOutOfRangeException>();
  }
}
