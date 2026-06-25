using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Meesles.Avalon;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Models;
using Xunit;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Tests;

public class SimInvariantTests {
  [Fact]
  public void InitializeWorld_CreatesExpectedPlayerHeroesAndBases() {
    var harness = SimHarness.CreateInitialized();

    harness.Count<Hero>().Should().Be(2);
    harness.Count<Base>().Should().Be(2);
    harness.Count<SpawnPoint>().Should().Be(2);

    UnitSnapshot[] units = GetUnits(harness);
    units.Should().HaveCount(4);
    units.Select(unit => unit.UnitId).Should().OnlyHaveUniqueItems();
    units.Select(unit => unit.UnitId).Should().BeEquivalentTo([1, 2, 3, 4]);
    units.Where(unit => unit.UnitTypeId == 1).Should().HaveCount(2);
    units.Where(unit => unit.UnitTypeId == 100).Should().HaveCount(2);

    GetPlayerSnapshots(harness)
        .Should()
        .BeEquivalentTo([
            new PlayerSnapshot(1, 1, FP64.Zero, FP64.Zero, 0),
            new PlayerSnapshot(2, 2, FP64.Zero, FP64.Zero, 0),
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
    var filter = frame.Filter<Player, UnitMoveTarget>();
    while (filter.Next(out var entity)) {
      ref readonly var player = ref frame.Get<Player>(entity);
      if (player.PlayerId == 1) {
        ref readonly var target = ref frame.Get<UnitMoveTarget>(entity);
        target.Target.x.Should().Be(FP64.One);
        target.Target.z.Should().Be(-FP64.One);
        player1HasTarget = true;
      }
      else {
        Assert.Fail($"Player {player.PlayerId} should not have a UnitMoveTarget");
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
    minions.Should().OnlyContain(minion => minion.WaveId == 0);
    minions.Count(minion => minion.TeamId == 1).Should().Be(rules.MinionsPerWave);
    minions.Count(minion => minion.TeamId == 2).Should().Be(rules.MinionsPerWave);
    minions.Should().OnlyContain(minion => minion.OwnerId == minion.TeamId);
    minions.Select(minion => minion.UnitId).Should().OnlyHaveUniqueItems();

    foreach (var teamGroup in minions.GroupBy(minion => minion.TeamId)) {
      var positions = teamGroup.Select(minion => minion.Position).ToArray();
      for (int a = 0; a < positions.Length; a++) {
        for (int b = a + 1; b < positions.Length; b++) {
          (positions[a] - positions[b]).sqrMagnitude.Should().BeGreaterThan(rules.MinionSpacing * rules.MinionSpacing);
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

    foreach (var teamGroup in minions.GroupBy(minion => minion.TeamId)) {
      var positions = teamGroup.Select(minion => minion.Position).ToArray();
      for (int a = 0; a < positions.Length; a++) {
        for (int b = a + 1; b < positions.Length; b++) {
          (positions[a] - positions[b]).sqrMagnitude.Should().BeGreaterThan(rules.MinionSpacing * rules.MinionSpacing);
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
    command.AddUnitId(teamOneMinion.UnitId);

    harness.Tick(command);

    var frame = harness.Frame;
    EntityRef minionEntity = default;
    var filter = frame.Filter<Minion, Unit>();
    while (filter.Next(out var entity)) {
      ref readonly var unit = ref frame.Get<Unit>(entity);
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
  public void SelectedMoveCommands_CanMoveHeroAndMinionTogetherThroughNavigation() {
    var harness = SimHarness.CreateInitialized();
    var rules = harness.AssetRegistry.Get<WaveRulesAsset>();

    for (int tick = 0; tick <= rules.FirstWaveDelayTicks; tick++)
      harness.Tick();

    UnitPositionSnapshot hero = GetUnitPositions(harness)
        .Single(unit => unit.TeamId == 1 && unit.UnitTypeId == 1);
    UnitPositionSnapshot minion = GetUnitPositions(harness)
        .First(unit => unit.TeamId == 1 && unit.UnitTypeId == SimulationSetup.MinionUnitTypeId);

    var command = SimHarness.MoveCommand(1, 0, FP64.Zero, FP64.Zero);
    command.AddUnitId(hero.UnitId);
    command.AddUnitId(minion.UnitId);

    harness.Tick(command);
    for (int tick = 0; tick < 12; tick++)
      harness.Tick();

    UnitPositionSnapshot movedHero = GetUnitPositions(harness).Single(unit => unit.UnitId == hero.UnitId);
    UnitPositionSnapshot movedMinion = GetUnitPositions(harness).Single(unit => unit.UnitId == minion.UnitId);

    (movedHero.Position - hero.Position).sqrMagnitude.Should().BeGreaterThan(FP64.Zero);
    (movedMinion.Position - minion.Position).sqrMagnitude.Should().BeGreaterThan(FP64.Zero);

    var frame = harness.Frame;
    var movingNavAgents = 0;
    var filter = frame.Filter<Unit, xpTURN.Klotho.Deterministic.Navigation.NavAgentComponent>();
    while (filter.Next(out var entity)) {
      ref readonly var unit = ref frame.Get<Unit>(entity);
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

    var command = SimHarness.MoveCommand(1, 0, FP64.Zero, FP64.Zero);
    command.AddUnitId(hero.UnitId);
    command.AddUnitId(minion.UnitId);

    harness.Tick(command);
    for (int tick = 0; tick < 800; tick++)
      harness.Tick();

    var frame = harness.Frame;
    frame.Has<UnitMoveTarget>(FindUnitEntity(harness, hero.UnitId)).Should().BeFalse();
    frame.Has<UnitMoveTarget>(FindUnitEntity(harness, minion.UnitId)).Should().BeFalse();

    UnitPositionSnapshot settledHero = GetUnitPositions(harness).Single(unit => unit.UnitId == hero.UnitId);
    UnitPositionSnapshot settledMinion = GetUnitPositions(harness).Single(unit => unit.UnitId == minion.UnitId);
    FPVector3 target = new FPVector3(FP64.Zero, FP64.Zero, FP64.Zero);

    (settledHero.Position - target).sqrMagnitude.Should().BeLessThan(FP64.FromInt(4));
    (settledMinion.Position - target).sqrMagnitude.Should().BeLessThan(FP64.FromInt(9));
    (settledHero.Position - settledMinion.Position).sqrMagnitude.Should().BeGreaterThan(FP64.Zero);
  }

  [Fact]
  public void Respawn_IsDeterministic() {
    var simA = SimHarness.CreateInitialized();
    var simB = SimHarness.CreateInitialized();

    ForcePlayerFall(simA, playerId: 1);
    ForcePlayerFall(simB, playerId: 1);

    simA.Tick(SimHarness.MoveCommand(1, 0, FP64.One, FP64.One));
    simB.Tick(SimHarness.MoveCommand(1, 0, FP64.One, FP64.One));

    simA.StateHash.Should().Be(simB.StateHash);

    PlayerSnapshot player = GetPlayerSnapshots(simA).Single(snapshot => snapshot.PlayerId == 1);
    player.Score.Should().Be(-1);
    player.LastInputH.Should().Be(FP64.Zero);
    player.LastInputV.Should().Be(FP64.Zero);

    PlayerTransformSnapshot transform = GetPlayerTransforms(simA).Single(snapshot => snapshot.PlayerId == 1);
    var frame = simA.Frame;
    transform.Position.Should().Be(SimulationSetup.GetHeroSpawnPositionForTeam(ref frame, teamId: 1));
  }

  private static UnitSnapshot[] GetUnits(SimHarness harness) {
    var frame = harness.Frame;
    var units = new List<UnitSnapshot>();
    var filter = frame.Filter<Unit>();
    while (filter.Next(out var entity)) {
      ref readonly var unit = ref frame.Get<Unit>(entity);
      units.Add(new UnitSnapshot(unit.UnitId, unit.UnitTypeId));
    }

    return units.OrderBy(unit => unit.UnitId).ToArray();
  }

  private static PlayerSnapshot[] GetPlayerSnapshots(SimHarness harness) {
    var frame = harness.Frame;
    var players = new List<PlayerSnapshot>();
    var filter = frame.Filter<Player, Team>();
    while (filter.Next(out var entity)) {
      ref readonly var player = ref frame.Get<Player>(entity);
      ref readonly var team = ref frame.Get<Team>(entity);
      players.Add(new PlayerSnapshot(
          player.PlayerId,
          team.TeamId,
          player.LastInputH,
          player.LastInputV,
          player.Score));
    }

    return players.OrderBy(player => player.PlayerId).ToArray();
  }

  private static PlayerTransformSnapshot[] GetPlayerTransforms(SimHarness harness) {
    var frame = harness.Frame;
    var transforms = new List<PlayerTransformSnapshot>();
    var filter = frame.Filter<Player, TransformComponent>();
    while (filter.Next(out var entity)) {
      ref readonly var player = ref frame.Get<Player>(entity);
      ref readonly var transform = ref frame.Get<TransformComponent>(entity);
      transforms.Add(new PlayerTransformSnapshot(player.PlayerId, transform.Position));
    }

    return transforms.OrderBy(transform => transform.PlayerId).ToArray();
  }

  private static MinionSnapshot[] GetMinions(SimHarness harness) {
    var frame = harness.Frame;
    var minions = new List<MinionSnapshot>();
    var filter = frame.Filter<Minion, Team, Unit, OwnerComponent, TransformComponent>();
    while (filter.Next(out var entity)) {
      ref readonly var minion = ref frame.Get<Minion>(entity);
      ref readonly var team = ref frame.Get<Team>(entity);
      ref readonly var unit = ref frame.Get<Unit>(entity);
      ref readonly var owner = ref frame.Get<OwnerComponent>(entity);
      ref readonly var transform = ref frame.Get<TransformComponent>(entity);
      minions.Add(new MinionSnapshot(minion.WaveId, team.TeamId, owner.OwnerId, unit.UnitId, transform.Position));
    }

    return minions.OrderBy(minion => minion.UnitId).ToArray();
  }

  private static UnitPositionSnapshot[] GetUnitPositions(SimHarness harness) {
    var frame = harness.Frame;
    var units = new List<UnitPositionSnapshot>();
    var filter = frame.Filter<Unit, Team, TransformComponent>();
    while (filter.Next(out var entity)) {
      ref readonly var unit = ref frame.Get<Unit>(entity);
      ref readonly var team = ref frame.Get<Team>(entity);
      ref readonly var transform = ref frame.Get<TransformComponent>(entity);
      units.Add(new UnitPositionSnapshot(unit.UnitId, unit.UnitTypeId, team.TeamId, transform.Position));
    }

    return units.OrderBy(unit => unit.UnitId).ToArray();
  }

  private static void ForcePlayerFall(SimHarness harness, int playerId) {
    var frame = harness.Frame;
    var stats = harness.AssetRegistry.Get<PlayerStatsAsset>();
    var filter = frame.Filter<Player, TransformComponent>();
    while (filter.Next(out var entity)) {
      ref readonly var player = ref frame.Get<Player>(entity);
      if (player.PlayerId != playerId)
        continue;

      ref var transform = ref frame.Get<TransformComponent>(entity);
      transform.Position = new FPVector3(FP64.Zero, stats.FallThresholdY - FP64.One, FP64.Zero);
      return;
    }
  }

  private static void MoveMinion(SimHarness harness, int unitId, FPVector3 position) {
    var frame = harness.Frame;
    var filter = frame.Filter<Minion, Unit, TransformComponent>();
    while (filter.Next(out var entity)) {
      ref readonly var unit = ref frame.Get<Unit>(entity);
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
    var filter = frame.Filter<Unit>();
    while (filter.Next(out var entity)) {
      ref readonly var unit = ref frame.Get<Unit>(entity);
      if (unit.UnitId == unitId)
        return entity;
    }

    Assert.Fail($"Unit {unitId} was not found.");
    return default;
  }

  private sealed record UnitSnapshot(int UnitId, int UnitTypeId);

  private sealed record PlayerSnapshot(int PlayerId, int TeamId, FP64 LastInputH, FP64 LastInputV, int Score);

  private sealed record PlayerTransformSnapshot(int PlayerId, FPVector3 Position);

  private sealed record MinionSnapshot(int WaveId, int TeamId, int OwnerId, int UnitId, FPVector3 Position);

  private sealed record UnitPositionSnapshot(int UnitId, int UnitTypeId, int TeamId, FPVector3 Position);
}
