using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Meesles.Avalon.Sim.Components;
using Meesles.Avalon.Sim.Factories;
using Meesles.Avalon.Sim.Navigation;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using Xunit;

namespace Meesles.Avalon.Sim.Tests;

public class TeamSpatialFilterTests {
  [Theory]
  [InlineData(0)]
  [InlineData(1)]
  [InlineData(2)]
  [InlineData(3)]
  public void FilteredQueries_MatchUnfilteredQueriesWithFriendlyEntriesRemoved(int seed) {
    var grid = new SpatialHashGrid(FP64.FromInt(5));
    var random = new Random(seed);
    var teams = new[] { int.MinValue, 0, 1, 2, 4, 63, 64, int.MaxValue };
    var teamByEntity = new Dictionary<EntityRef, int>();
    for (var i = 0; i < 256; i++) {
      var entity = new EntityRef(i, 1);
      var team = seed == 0 ? 1 : teams[random.Next(teams.Length)];
      teamByEntity.Add(entity, team);
      grid.Insert(entity, Point(random.Next(-20, 21), random.Next(-20, 21)), team);
    }

    var all = new List<EntityRef>();
    var filtered = new List<EntityRef>();
    foreach (var team in teams)
      foreach (var radius in new[] { -1, 0, 1, 5, 17 })
        foreach (var center in new[] { Point(-5, 0), Point(0, 0), Point(5, 5) }) {
          grid.QueryRadius(center, FP64.FromInt(radius), all);
          grid.QueryRadius(center, FP64.FromInt(radius), filtered, team);
          filtered.Should().Equal(all.Where(entity => teamByEntity[entity] != team));
        }
  }

  [Fact]
  public void Filtering_PreservesRadiusBoundaryAndDefaultNeutralTeam() {
    var grid = new SpatialHashGrid(FP64.FromInt(10));
    var friendly = new EntityRef(0, 1);
    var boundary = new EntityRef(1, 1);
    var neutral = new EntityRef(2, 1);
    var outside = new EntityRef(3, 1);
    grid.Insert(friendly, Point(0, 0), 1);
    grid.Insert(boundary, Point(3, 4), 2);
    grid.Insert(neutral, Point(-3, -4));
    grid.Insert(outside, Point(4, 4), 2);
    var result = new List<EntityRef>();
    grid.QueryRadius(Point(0, 0), FP64.FromInt(5), result, 1);
    result.Should().BeEquivalentTo(new[] { boundary, neutral });
    grid.Clear();
    grid.Insert(friendly, Point(0, 0), 1);
    grid.QueryRadius(Point(0, 0), FP64.FromInt(5), result, 1);
    result.Should().BeEmpty();
  }

  [Fact]
  public void UnitIndex_RebuildsMovementTeamChangesAndDiscardedPrediction() {
    var harness = SimHarness.CreateInitialized(spawnHeroesNow: false);
    var frame = harness.Frame;
    var original = SpawnUnit(ref frame, 0, 2);
    var index = new UnitSpatialIndex();
    var nearby = new List<EntityRef>();
    harness.Simulation.SaveSnapshot();

    index.Update(ref frame);
    index.Grid.QueryRadius(Point(0, 0), FP64.One, nearby, 1);
    nearby.Should().Contain(original);

    frame.Get<Team>(original).TeamId = 1;
    index.Update(ref frame);
    index.Grid.QueryRadius(Point(0, 0), FP64.One, nearby, 1);
    nearby.Should().NotContain(original);

    frame.Get<Team>(original).TeamId = 2;
    frame.Get<TransformComponent>(original).Position = new FPVector3(FP64.FromInt(20), FP64.Zero, FP64.Zero);
    var discarded = SpawnUnit(ref frame, 0, 3);
    index.Update(ref frame);
    index.Grid.QueryRadius(Point(0, 0), FP64.One, nearby, 1);
    nearby.Should().Contain(discarded).And.NotContain(original);

    harness.Simulation.Rollback(frame.Tick);
    frame = harness.Frame;
    index.Update(ref frame);
    index.Grid.QueryRadius(Point(0, 0), FP64.One, nearby, 1);
    nearby.Should().Contain(original).And.NotContain(discarded);
  }

  [Fact]
  public void Acquisition_PreservesPriorityVisibilityHealthAndStableTies() {
    var harness = SimHarness.CreateInitialized(spawnHeroesNow: false);
    var frame = harness.Frame;
    var attacker = SpawnUnit(ref frame, 0, 1);
    frame.Add(attacker, new Combat());
    frame.Get<Stats>(attacker).Set(StatType.AcquisitionRange, FP64.FromInt(9));
    SpawnUnit(ref frame, .1, 1);
    var expected = SpawnUnit(ref frame, 2, 2);
    SpawnUnit(ref frame, -2, 3);
    var turret = SpawnUnit(ref frame, .5, 2);
    frame.Remove<Minion>(turret);
    frame.Add(turret, new Turret());
    var dead = SpawnUnit(ref frame, .2, 2);
    frame.Get<Health>(dead).Current = FP64.Zero;
    var hidden = SpawnUnit(ref frame, .3, 2);
    frame.Get<UnitVisibility>(hidden).Teams = 0;
    SpawnUnit(ref frame, 5, 0);

    var index = new UnitSpatialIndex();
    index.Update(ref frame);
    new TargetAcquisitionSystem(index).Update(ref frame);
    frame.GetReadOnly<AttackTargetUnitId>(attacker).TargetUnitId.Should()
      .Be(frame.GetReadOnly<UnitIdentity>(expected).UnitId);
  }

  private static FPVector2 Point(int x, int z) => new(FP64.FromInt(x), FP64.FromInt(z));

  private static EntityRef SpawnUnit(ref Frame frame, double x, int team) {
    var entity = frame.CreateEntity();
    frame.Add(entity, TransformFactory.At(new FPVector3(FP64.FromDouble(x), FP64.Zero, FP64.Zero)));
    frame.Add(entity, new UnitIdentity { UnitId = UnitLookup.NextUnitId(ref frame), UnitTypeId = SimulationSetup.MinionUnitTypeId });
    frame.Add(entity, new Team { TeamId = team });
    frame.Add(entity, new Health(FP64.FromInt(10000)));
    frame.Add(entity, new Minion());
    frame.Add(entity, Stats.Create());
    frame.Add(entity, new UnitVisibility { Teams = ulong.MaxValue });
    return entity;
  }
}
