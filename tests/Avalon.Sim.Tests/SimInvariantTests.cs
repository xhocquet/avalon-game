using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Meesles.Avalon;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using Xunit;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Tests;

public class SimInvariantTests {
  [Fact]
  public void InitializeWorld_CreatesExpectedPlayerHeroesCrystalsAndTurrets() {
    var harness = SimHarness.CreateInitialized();

    harness.Count<Hero>().Should().Be(2);
    harness.Count<Crystal>().Should().Be(2);
    harness.Count<Turret>().Should().Be(4);
    harness.Count<SpawnPoint>().Should().Be(2);
    harness.Count<Controllable>().Should().Be(2);
    GetCrystals(harness)
        .Should()
        .BeEquivalentTo([
            new StructureSnapshot(1, 1),
            new StructureSnapshot(2, 2),
        ]);

    UnitSnapshot[] units = GetUnits(harness);
    units.Should().HaveCount(8);
    units.Select(unit => unit.UnitId).Should().OnlyHaveUniqueItems();
    units.Select(unit => unit.UnitId).Should().BeEquivalentTo([1, 2, 3, 4, 5, 6, 7, 8]);
    units.Where(unit => unit.UnitTypeId == 1).Should().HaveCount(2);
    units.Where(unit => unit.UnitTypeId == 100).Should().HaveCount(2);
    units.Where(unit => unit.UnitTypeId == 101).Should().HaveCount(4);
    harness.Count<Health>().Should().Be(8);
    harness.Count<Combat>().Should().Be(6);

    GetPlayerSnapshots(harness)
        .Should()
        .BeEquivalentTo([
            new PlayerSnapshot(1, 1, 0),
            new PlayerSnapshot(2, 2, 0),
        ]);
  }

  [Fact]
  public void InitialWorld_HashIsStable() {
    long[] hashes = Enumerable
        .Range(0, 5)
        .Select(_ => SimHarness.CreateInitialized().StateHash)
        .ToArray();

    hashes.Should().OnlyContain(hash => hash == hashes[0]);
  }

  [Fact]
  public void MoveCommands_AffectOnlyOwningPlayer() {
    var harness = SimHarness.CreateInitialized();

    harness.Tick(SimHarness.MoveCommand(1, 0, FP64.One, -FP64.One));

    var frame = harness.Frame;
    bool player1HasTarget = false;
    var filter = frame.Filter<Hero, UnitMoveTarget>();
    while (filter.Next(out var entity)) {
      ref readonly var hero = ref frame.Get<Hero>(entity);
      if (hero.PlayerId == 1) {
        ref readonly var target = ref frame.Get<UnitMoveTarget>(entity);
        target.Target.x.Should().Be(FP64.One);
        target.Target.z.Should().Be(-FP64.One);
        player1HasTarget = true;
      }
      else {
        Assert.Fail($"Player {hero.PlayerId} should not have a UnitMoveTarget");
      }
    }
    player1HasTarget.Should().BeTrue("Player 1 should have a move target after MoveCommand");
  }

  [Fact]
  public void WaveSpawn_IsDeterministic() {
    var simA = SimHarness.CreateInitialized();
    var simB = SimHarness.CreateInitialized();
    var rules = simA.AssetRegistry.Get<WaveRulesAsset>();

    for (int tick = 0; tick <= rules.FirstWaveDelayTicks; tick++) {
      simA.Tick();
      simB.Tick();
    }

    simA.StateHash.Should().Be(simB.StateHash);

    MinionSnapshot[] minions = GetMinions(simA);
    minions.Should().HaveCount(rules.MinionsPerWave * 2);
    simA.Count<Controllable>().Should().Be(2 + minions.Length);
    minions.Should().OnlyContain(minion => minion.WaveId == 0);
    minions.Count(minion => minion.TeamId == 1).Should().Be(rules.MinionsPerWave);
    minions.Count(minion => minion.TeamId == 2).Should().Be(rules.MinionsPerWave);
    minions.Select(minion => minion.UnitId).Should().OnlyHaveUniqueItems();

    // Minions pack into distinct hex slots. The spawner rejects any slot within half the spacing
    // of an existing minion (the occupancy radius), so that half-spacing is the separation
    // guarantee — neighbouring hex slots sit right around the full spacing.
    var minSeparation = rules.MinionSpacing * FP64.Half;
    foreach (var teamGroup in minions.GroupBy(minion => minion.TeamId)) {
      var positions = teamGroup.Select(minion => minion.Position).ToArray();
      for (int a = 0; a < positions.Length; a++) {
        for (int b = a + 1; b < positions.Length; b++) {
          (positions[a] - positions[b]).sqrMagnitude.Should().BeGreaterThan(minSeparation * minSeparation);
        }
      }
    }
  }

  [Fact]
  public void WaveSpawn_FillsOccupiedSlotsOutward() {
    var harness = SimHarness.CreateInitialized();
    var rules = harness.AssetRegistry.Get<WaveRulesAsset>();

    int waveCount = 4;
    int finalSpawnTick = rules.FirstWaveDelayTicks + rules.SpawnIntervalTicks * (waveCount - 1);
    for (int tick = 0; tick <= finalSpawnTick; tick++)
      harness.Tick();

    MinionSnapshot[] minions = GetMinions(harness);
    minions.Count(minion => minion.TeamId == 1).Should().Be(waveCount);
    minions.Count(minion => minion.TeamId == 2).Should().Be(waveCount);

    // Minions pack into distinct hex slots. The spawner rejects any slot within half the spacing
    // of an existing minion (the occupancy radius), so that half-spacing is the separation
    // guarantee — neighbouring hex slots sit right around the full spacing.
    var minSeparation = rules.MinionSpacing * FP64.Half;
    foreach (var teamGroup in minions.GroupBy(minion => minion.TeamId)) {
      var positions = teamGroup.Select(minion => minion.Position).ToArray();
      for (int a = 0; a < positions.Length; a++) {
        for (int b = a + 1; b < positions.Length; b++) {
          (positions[a] - positions[b]).sqrMagnitude.Should().BeGreaterThan(minSeparation * minSeparation);
        }
      }
    }
  }

  [Fact]
  public void WaveSpawn_ReusesSlotAfterMinionMovesAway() {
    var harness = SimHarness.CreateInitialized();
    var rules = harness.AssetRegistry.Get<WaveRulesAsset>();

    int waveCount = 4;
    int finalSpawnTick = rules.FirstWaveDelayTicks + rules.SpawnIntervalTicks * (waveCount - 1);
    for (int tick = 0; tick <= finalSpawnTick; tick++)
      harness.Tick();

    MinionSnapshot firstMinion = GetMinions(harness).First(minion => minion.TeamId == 1);
    FPVector3 originalPosition = firstMinion.Position;
    MoveMinion(harness, firstMinion.UnitId, originalPosition + new FPVector3(rules.MinionSpacing * FP64.FromInt(4), FP64.Zero, FP64.Zero));

    for (int tick = 0; tick < rules.SpawnIntervalTicks; tick++)
      harness.Tick();

    MinionSnapshot reusedSlotMinion = GetMinions(harness)
        .Where(minion => minion.TeamId == 1 && minion.UnitId != firstMinion.UnitId)
        .OrderBy(minion => (minion.Position - originalPosition).sqrMagnitude)
        .First();

    (reusedSlotMinion.Position - originalPosition).sqrMagnitude.Should().Be(FP64.Zero);
  }

  [Fact]
  public void SelectedMoveCommands_AffectOwnedTeamMinions() {
    var harness = SimHarness.CreateInitialized();
    var rules = harness.AssetRegistry.Get<WaveRulesAsset>();

    for (int tick = 0; tick <= rules.FirstWaveDelayTicks; tick++)
      harness.Tick();

    MinionSnapshot teamOneMinion = GetMinions(harness).First(minion => minion.TeamId == 1);
    var command = SimHarness.MoveCommand(1, 0, FP64.One, -FP64.One);
    command.UnitIds.Add(teamOneMinion.UnitId);

    harness.Tick(command);

    var frame = harness.Frame;
    EntityRef minionEntity = default;
    var filter = frame.Filter<Minion, UnitIdComponent>();
    while (filter.Next(out var entity)) {
      ref readonly var unit = ref frame.Get<UnitIdComponent>(entity);
      if (unit.UnitId != teamOneMinion.UnitId) continue;

      minionEntity = entity;
      break;
    }

    minionEntity.IsValid.Should().BeTrue();
    frame.Has<UnitMoveTarget>(minionEntity).Should().BeTrue();
    ref readonly var target = ref frame.Get<UnitMoveTarget>(minionEntity);
    target.Target.x.Should().Be(FP64.One);
    target.Target.z.Should().Be(-FP64.One);
  }

  [Fact]
  public void SelectedMoveCommands_DoNotMoveOwnTeamTurrets() {
    var harness = SimHarness.CreateInitialized();
    UnitPositionSnapshot turret = GetUnitPositions(harness)
        .First(unit => unit.TeamId == 1 && unit.UnitTypeId == SimulationSetup.TurretUnitTypeId);

    var command = SimHarness.MoveCommand(1, 0, FP64.One, -FP64.One);
    command.UnitIds.Add(turret.UnitId);

    harness.Tick(command);

    var frame = harness.Frame;
    EntityRef turretEntity = FindUnitEntity(harness, turret.UnitId);
    frame.Has<Controllable>(turretEntity).Should().BeFalse();
    frame.Has<UnitMoveTarget>(turretEntity).Should().BeFalse();
    frame.GetReadOnly<TransformComponent>(turretEntity).Position.Should().Be(turret.Position);
  }

  [Fact]
  public void SelectedMoveCommands_CanMoveHeroAndMinionTogetherThroughNavigation() {
    var harness = SimHarness.CreateInitialized();
    var rules = harness.AssetRegistry.Get<WaveRulesAsset>();

    for (int tick = 0; tick <= rules.FirstWaveDelayTicks; tick++)
      harness.Tick();

    UnitPositionSnapshot hero = GetUnitPositions(harness)
        .Single(unit => unit.TeamId == 1 && unit.UnitTypeId == 1);
    UnitPositionSnapshot minion = GetUnitPositions(harness)
        .First(unit => unit.TeamId == 1 && unit.UnitTypeId == SimulationSetup.MinionUnitTypeId);

    // (-10, 10) is open ground on the spawn->centre path. Avoid (0, 0): the map centre is a
    // navmesh hole (central structure), so a group move there strands the A* hero and piles
    // minions at the rim — not what this test is exercising.
    var command = SimHarness.MoveCommand(1, 0, FP64.FromInt(-10), FP64.FromInt(10));
    command.UnitIds.Add(hero.UnitId);
    command.UnitIds.Add(minion.UnitId);

    harness.Tick(command);
    for (int tick = 0; tick < 12; tick++)
      harness.Tick();

    UnitPositionSnapshot movedHero = GetUnitPositions(harness).Single(unit => unit.UnitId == hero.UnitId);
    UnitPositionSnapshot movedMinion = GetUnitPositions(harness).Single(unit => unit.UnitId == minion.UnitId);

    (movedHero.Position - hero.Position).sqrMagnitude.Should().BeGreaterThan(FP64.Zero);
    (movedMinion.Position - minion.Position).sqrMagnitude.Should().BeGreaterThan(FP64.Zero);

    var frame = harness.Frame;
    var movingNavAgents = 0;
    var filter = frame.Filter<UnitIdComponent, xpTURN.Klotho.Deterministic.Navigation.NavAgentComponent>();
    while (filter.Next(out var entity)) {
      ref readonly var unit = ref frame.Get<UnitIdComponent>(entity);
      if (unit.UnitId != hero.UnitId && unit.UnitId != minion.UnitId)
        continue;

      ref readonly var nav = ref frame.Get<xpTURN.Klotho.Deterministic.Navigation.NavAgentComponent>(entity);
      nav.Velocity.sqrMagnitude.Should().BeGreaterThan(FP64.Zero);
      movingNavAgents++;
    }

    movingNavAgents.Should().Be(2);
  }

  [Fact]
  public void SelectedMoveCommands_FormationGroupSettlesNearDestination() {
    var harness = SimHarness.CreateInitialized();
    var rules = harness.AssetRegistry.Get<WaveRulesAsset>();

    for (int tick = 0; tick <= rules.FirstWaveDelayTicks; tick++)
      harness.Tick();

    UnitPositionSnapshot hero = GetUnitPositions(harness)
        .Single(unit => unit.TeamId == 1 && unit.UnitTypeId == 1);
    UnitPositionSnapshot minion = GetUnitPositions(harness)
        .First(unit => unit.TeamId == 1 && unit.UnitTypeId == SimulationSetup.MinionUnitTypeId);
    rules.SpawnIntervalTicks = int.MaxValue;

    // (-10, 10) is open ground; (0, 0) is inside the central navmesh hole and unreachable.
    var command = SimHarness.MoveCommand(1, 0, FP64.FromInt(-10), FP64.FromInt(10));
    command.UnitIds.Add(hero.UnitId);
    command.UnitIds.Add(minion.UnitId);

    harness.Tick(command);
    for (int tick = 0; tick < 800; tick++)
      harness.Tick();

    var frame = harness.Frame;
    frame.Has<UnitMoveTarget>(FindUnitEntity(harness, hero.UnitId)).Should().BeFalse();
    frame.Has<UnitMoveTarget>(FindUnitEntity(harness, minion.UnitId)).Should().BeFalse();

    UnitPositionSnapshot settledHero = GetUnitPositions(harness).Single(unit => unit.UnitId == hero.UnitId);
    UnitPositionSnapshot settledMinion = GetUnitPositions(harness).Single(unit => unit.UnitId == minion.UnitId);
    FPVector3 target = new FPVector3(FP64.FromInt(-10), FP64.Zero, FP64.FromInt(10));

    (settledHero.Position - target).sqrMagnitude.Should().BeLessThan(FP64.FromInt(4));
    (settledMinion.Position - target).sqrMagnitude.Should().BeLessThan(FP64.FromInt(9));
    (settledHero.Position - settledMinion.Position).sqrMagnitude.Should().BeGreaterThan(FP64.Zero);
  }

  [Fact]
  public void SelectedMoveCommands_CanMoveSeveralMinionsThroughNavigation() {
    var harness = SimHarness.CreateInitialized();
    var rules = harness.AssetRegistry.Get<WaveRulesAsset>();
    rules.MinionsPerWave = 6;
    rules.SpawnIntervalTicks = int.MaxValue;

    for (int tick = 0; tick <= rules.FirstWaveDelayTicks; tick++)
      harness.Tick();

    MinionSnapshot[] startMinions = GetMinions(harness)
        .Where(minion => minion.TeamId == 1)
        .OrderBy(minion => minion.UnitId)
        .ToArray();
    startMinions.Should().HaveCount(6);

    var command = SimHarness.MoveCommand(1, 0, FP64.Zero, FP64.Zero);
    foreach (var minion in startMinions)
      command.UnitIds.Add(minion.UnitId);

    harness.Tick(command);
    for (int tick = 0; tick < 60; tick++)
      harness.Tick();

    MinionSnapshot[] movedMinions = GetMinions(harness)
        .Where(minion => minion.TeamId == 1)
        .OrderBy(minion => minion.UnitId)
        .ToArray();

    for (int i = 0; i < startMinions.Length; i++)
      (movedMinions[i].Position - startMinions[i].Position).sqrMagnitude.Should().BeGreaterThan(FP64.Zero);
  }

  [Fact]
  public void NavigationAgents_WithSharedTargetStillMove() {
    var harness = SimHarness.CreateInitialized();
    var rules = harness.AssetRegistry.Get<WaveRulesAsset>();
    rules.MinionsPerWave = 6;
    rules.SpawnIntervalTicks = int.MaxValue;

    for (int tick = 0; tick <= rules.FirstWaveDelayTicks; tick++)
      harness.Tick();

    MinionSnapshot[] startMinions = GetMinions(harness)
        .Where(minion => minion.TeamId == 1)
        .OrderBy(minion => minion.UnitId)
        .ToArray();
    startMinions.Should().HaveCount(6);

    FPVector3 sharedTarget = FPVector3.Zero;
    foreach (var minion in startMinions) {
      EntityRef entity = FindUnitEntity(harness, minion.UnitId);
      harness.Frame.Add(entity, new UnitMoveTarget { Target = sharedTarget });
    }

    for (int tick = 0; tick < 60; tick++)
      harness.Tick();

    MinionSnapshot[] movedMinions = GetMinions(harness)
        .Where(minion => minion.TeamId == 1)
        .OrderBy(minion => minion.UnitId)
        .ToArray();

    for (int i = 0; i < startMinions.Length; i++)
      (movedMinions[i].Position - startMinions[i].Position).sqrMagnitude.Should().BeGreaterThan(FP64.Zero);
  }

  [Fact]
  public void NavigationAgents_OverlappedAgentsStillMoveTowardSharedTarget() {
    var harness = SimHarness.CreateInitialized();
    var rules = harness.AssetRegistry.Get<WaveRulesAsset>();
    rules.MinionsPerWave = 2;
    rules.SpawnIntervalTicks = int.MaxValue;

    for (int tick = 0; tick <= rules.FirstWaveDelayTicks; tick++)
      harness.Tick();

    MinionSnapshot[] startMinions = GetMinions(harness)
        .Where(minion => minion.TeamId == 1)
        .OrderBy(minion => minion.UnitId)
        .ToArray();
    startMinions.Should().HaveCount(2);

    FPVector3 overlapPosition = startMinions[0].Position;
    FPVector3 sharedTarget = FPVector3.Zero;
    foreach (var minion in startMinions) {
      EntityRef entity = FindUnitEntity(harness, minion.UnitId);
      ref var transform = ref harness.Frame.Get<TransformComponent>(entity);
      transform.Position = overlapPosition;
      ref var nav = ref harness.Frame.Get<xpTURN.Klotho.Deterministic.Navigation.NavAgentComponent>(entity);
      xpTURN.Klotho.Deterministic.Navigation.NavAgentComponent.Init(ref nav, overlapPosition);
      harness.Frame.Add(entity, new UnitMoveTarget { Target = sharedTarget });
    }

    FP64 initialTargetDistance = (sharedTarget - overlapPosition).sqrMagnitude;
    for (int tick = 0; tick < 60; tick++)
      harness.Tick();

    MinionSnapshot[] movedMinions = GetMinions(harness)
        .Where(minion => minion.TeamId == 1)
        .OrderBy(minion => minion.UnitId)
        .ToArray();

    foreach (var minion in movedMinions) {
      (minion.Position - overlapPosition).sqrMagnitude.Should().BeGreaterThan(FP64.Zero);
      (sharedTarget - minion.Position).sqrMagnitude.Should().BeLessThan(initialTargetDistance);
    }

    (movedMinions[0].Position - movedMinions[1].Position).sqrMagnitude.Should().BeGreaterThan(FP64.Zero);
  }

  [Fact]
  public void Respawn_IsDeterministic() {
    var simA = SimHarness.CreateInitialized();
    var simB = SimHarness.CreateInitialized();

    SetPlayerHealth(simA, playerId: 1, current: 0);
    SetPlayerHealth(simB, playerId: 1, current: 0);

    simA.Tick(SimHarness.MoveCommand(1, 0, FP64.One, FP64.One));
    simB.Tick(SimHarness.MoveCommand(1, 0, FP64.One, FP64.One));

    simA.StateHash.Should().Be(simB.StateHash);

    PlayerSnapshot player = GetPlayerSnapshots(simA).Single(snapshot => snapshot.PlayerId == 1);
    player.Score.Should().Be(-1);
    simA.Frame.Has<PendingRespawn>(FindHeroEntity(simA, playerId: 1)).Should().BeTrue();

    PlayerTransformSnapshot transform = GetPlayerTransforms(simA).Single(snapshot => snapshot.PlayerId == 1);
    var frame = simA.Frame;
    transform.Position.Should().Be(SimulationSetup.GetHeroSpawnPositionForTeam(ref frame, teamId: 1));
  }

  [Fact]
  public void PlayerDeath_EmitsEventsAndRespawnsAfterDelay() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    EntityRef hero = FindHeroEntity(harness, playerId: 1);
    int unitId = frame.GetReadOnly<UnitIdComponent>(hero).UnitId;
    int delayTicks = GetRespawnDelayTicks(frame);
    frame.Get<Health>(hero).Current = 0;

    var collector = new EventCollector();
    collector.BeginTick(12);
    frame.EventRaiser = collector;

    var system = new RespawnSystem();
    system.Update(ref frame);

    hero = FindHeroEntity(harness, playerId: 1);
    frame.Has<PendingRespawn>(hero).Should().BeTrue();
    frame.GetReadOnly<Health>(hero).Current.Should().Be(0);
    frame.GetReadOnly<Player>(hero).Score.Should().Be(-1);

    collector.Count.Should().Be(1);
    var died = collector.Collected[0].Should().BeOfType<PlayerDiedEvent>().Subject;
    died.Tick.Should().Be(12);
    died.PlayerId.Should().Be(1);
    died.TeamId.Should().Be(1);
    died.UnitId.Should().Be(unitId);
    died.RespawnDelayTicks.Should().Be(delayTicks);

    for (int tick = 0; tick < delayTicks - 1; tick++)
      system.Update(ref frame);

    frame.Has<PendingRespawn>(hero).Should().BeTrue();
    frame.GetReadOnly<Health>(hero).Current.Should().Be(0);

    collector.BeginTick(12 + delayTicks);
    system.Update(ref frame);

    hero = FindHeroEntity(harness, playerId: 1);
    frame.GetReadOnly<Health>(hero).Current.Should().Be(frame.GetReadOnly<StatsComponent>(hero).MaxHealth);
    frame.Has<PendingRespawn>(hero).Should().BeFalse();
    FPVector3 spawnPosition = SimulationSetup.GetHeroSpawnPositionForTeam(ref frame, teamId: 1);
    frame.GetReadOnly<TransformComponent>(hero).Position.Should().Be(spawnPosition);

    collector.Count.Should().Be(1);
    var respawned = collector.Collected[0].Should().BeOfType<PlayerRespawnedEvent>().Subject;
    respawned.Tick.Should().Be(12 + delayTicks);
    respawned.PlayerId.Should().Be(1);
    respawned.TeamId.Should().Be(1);
    respawned.UnitId.Should().Be(unitId);
    respawned.Position.Should().Be(spawnPosition);
  }

  private static UnitSnapshot[] GetUnits(SimHarness harness) {
    var frame = harness.Frame;
    var units = new List<UnitSnapshot>();
    var filter = frame.Filter<UnitIdComponent>();
    while (filter.Next(out var entity)) {
      ref readonly var unit = ref frame.Get<UnitIdComponent>(entity);
      units.Add(new UnitSnapshot(unit.UnitId, unit.UnitTypeId));
    }

    return units.OrderBy(unit => unit.UnitId).ToArray();
  }

  private static StructureSnapshot[] GetCrystals(SimHarness harness) {
    var frame = harness.Frame;
    var crystals = new List<StructureSnapshot>();
    var filter = frame.Filter<Crystal, TeamComponent>();
    while (filter.Next(out var entity)) {
      ref readonly var crystal = ref frame.Get<Crystal>(entity);
      ref readonly var team = ref frame.Get<TeamComponent>(entity);
      crystals.Add(new StructureSnapshot(crystal.CrystalId, team.TeamId));
    }

    return crystals.OrderBy(crystal => crystal.Id).ToArray();
  }

  private static PlayerSnapshot[] GetPlayerSnapshots(SimHarness harness) {
    var frame = harness.Frame;
    var players = new List<PlayerSnapshot>();
    var filter = frame.Filter<Hero, Player, TeamComponent>();
    while (filter.Next(out var entity)) {
      ref readonly var hero = ref frame.Get<Hero>(entity);
      ref readonly var player = ref frame.Get<Player>(entity);
      ref readonly var team = ref frame.Get<TeamComponent>(entity);
      players.Add(new PlayerSnapshot(
          hero.PlayerId,
          team.TeamId,
          player.Score));
    }

    return players.OrderBy(player => player.PlayerId).ToArray();
  }

  private static PlayerTransformSnapshot[] GetPlayerTransforms(SimHarness harness) {
    var frame = harness.Frame;
    var transforms = new List<PlayerTransformSnapshot>();
    var filter = frame.Filter<Hero, TransformComponent>();
    while (filter.Next(out var entity)) {
      ref readonly var hero = ref frame.Get<Hero>(entity);
      ref readonly var transform = ref frame.Get<TransformComponent>(entity);
      transforms.Add(new PlayerTransformSnapshot(hero.PlayerId, transform.Position));
    }

    return transforms.OrderBy(transform => transform.PlayerId).ToArray();
  }

  private static MinionSnapshot[] GetMinions(SimHarness harness) {
    var frame = harness.Frame;
    var minions = new List<MinionSnapshot>();
    var filter = frame.Filter<Minion, TeamComponent, UnitIdComponent, TransformComponent>();
    while (filter.Next(out var entity)) {
      ref readonly var minion = ref frame.Get<Minion>(entity);
      ref readonly var team = ref frame.Get<TeamComponent>(entity);
      ref readonly var unit = ref frame.Get<UnitIdComponent>(entity);
      ref readonly var transform = ref frame.Get<TransformComponent>(entity);
      minions.Add(new MinionSnapshot(minion.WaveId, team.TeamId, unit.UnitId, transform.Position));
    }

    return minions.OrderBy(minion => minion.UnitId).ToArray();
  }

  private static UnitPositionSnapshot[] GetUnitPositions(SimHarness harness) {
    var frame = harness.Frame;
    var units = new List<UnitPositionSnapshot>();
    var filter = frame.Filter<UnitIdComponent, TeamComponent, TransformComponent>();
    while (filter.Next(out var entity)) {
      ref readonly var unit = ref frame.Get<UnitIdComponent>(entity);
      ref readonly var team = ref frame.Get<TeamComponent>(entity);
      ref readonly var transform = ref frame.Get<TransformComponent>(entity);
      units.Add(new UnitPositionSnapshot(unit.UnitId, unit.UnitTypeId, team.TeamId, transform.Position));
    }

    return units.OrderBy(unit => unit.UnitId).ToArray();
  }

  private static void SetPlayerHealth(SimHarness harness, int playerId, int current) {
    var frame = harness.Frame;
    var filter = frame.Filter<Hero, Health>();
    while (filter.Next(out var entity)) {
      ref readonly var hero = ref frame.Get<Hero>(entity);
      if (hero.PlayerId != playerId)
        continue;

      ref var health = ref frame.Get<Health>(entity);
      health.Current = current;
      return;
    }
  }

  private static void MoveMinion(SimHarness harness, int unitId, FPVector3 position) {
    var frame = harness.Frame;
    var filter = frame.Filter<Minion, UnitIdComponent, TransformComponent>();
    while (filter.Next(out var entity)) {
      ref readonly var unit = ref frame.Get<UnitIdComponent>(entity);
      if (unit.UnitId != unitId)
        continue;

      ref var transform = ref frame.Get<TransformComponent>(entity);
      transform.Position = position;
      return;
    }

    Assert.Fail($"Minion unit {unitId} was not found.");
  }

  private static EntityRef FindUnitEntity(SimHarness harness, int unitId) {
    var frame = harness.Frame;
    var filter = frame.Filter<UnitIdComponent>();
    while (filter.Next(out var entity)) {
      ref readonly var unit = ref frame.Get<UnitIdComponent>(entity);
      if (unit.UnitId == unitId)
        return entity;
    }

    Assert.Fail($"Unit {unitId} was not found.");
    return default;
  }

  private static EntityRef FindHeroEntity(SimHarness harness, int playerId) {
    var frame = harness.Frame;
    var filter = frame.Filter<Hero>();
    while (filter.Next(out var entity)) {
      ref readonly var hero = ref frame.Get<Hero>(entity);
      if (hero.PlayerId == playerId)
        return entity;
    }

    Assert.Fail($"Hero for player {playerId} was not found.");
    return default;
  }

  private static int GetRespawnDelayTicks(Frame frame) {
    return (5000 + frame.DeltaTimeMs - 1) / frame.DeltaTimeMs;
  }

  private record UnitSnapshot(int UnitId, int UnitTypeId);

  private record StructureSnapshot(int Id, int TeamId);

  private record PlayerSnapshot(int PlayerId, int TeamId, int Score);

  private record PlayerTransformSnapshot(int PlayerId, FPVector3 Position);

  private record MinionSnapshot(int WaveId, int TeamId, int UnitId, FPVector3 Position);

  private record UnitPositionSnapshot(int UnitId, int UnitTypeId, int TeamId, FPVector3 Position);
}
