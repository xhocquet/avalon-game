using System;
using System.Collections.Generic;
using FluentAssertions;
using Xunit;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Tests;

public class SpatialHashGridTests {
  private static readonly FP64 CellSize = FP64.FromInt(5);

  private static FPVector2 V(int x, int z) => new(FP64.FromInt(x), FP64.FromInt(z));

  [Fact]
  public void Constructor_RejectsNonPositiveCellSize() {
    Action zero = () => new SpatialHashGrid(FP64.Zero);
    Action negative = () => new SpatialHashGrid(FP64.FromInt(-1));

    zero.Should().Throw<ArgumentOutOfRangeException>();
    negative.Should().Throw<ArgumentOutOfRangeException>();
  }

  [Fact]
  public void QueryRadius_OnEmptyGrid_ReturnsNoResults() {
    var grid = new SpatialHashGrid(CellSize);
    var results = new List<EntityRef>();

    grid.QueryRadius(V(0, 0), FP64.FromInt(10), results);

    results.Should().BeEmpty();
  }

  [Fact]
  public void QueryRadius_FindsEntityWithinRadiusInSameCell() {
    var grid = new SpatialHashGrid(CellSize);
    var entity = new EntityRef(1, 1);
    grid.Insert(entity, V(1, 1));
    var results = new List<EntityRef>();

    grid.QueryRadius(V(0, 0), FP64.FromInt(3), results);

    results.Should().ContainSingle().Which.Should().Be(entity);
  }

  [Fact]
  public void QueryRadius_ExcludesEntityOutsideRadius_EvenInSameCell() {
    var grid = new SpatialHashGrid(CellSize);
    grid.Insert(new EntityRef(1, 1), V(4, 4));
    var results = new List<EntityRef>();

    // Same 5x5 cell as (4,4), but Euclidean distance from origin exceeds the radius.
    grid.QueryRadius(V(0, 0), FP64.FromInt(3), results);

    results.Should().BeEmpty();
  }

  [Fact]
  public void QueryRadius_FindsEntityAcrossCellBoundary() {
    var grid = new SpatialHashGrid(CellSize);
    // Cell size 5: this entity lands in the neighboring cell, not the query's origin cell.
    var entity = new EntityRef(1, 1);
    grid.Insert(entity, V(6, 0));
    var results = new List<EntityRef>();

    grid.QueryRadius(V(0, 0), FP64.FromInt(6), results);

    results.Should().Contain(entity);
  }

  [Fact]
  public void QueryRadius_IncludesEntityExactlyOnRadiusBoundary() {
    var grid = new SpatialHashGrid(CellSize);
    var entity = new EntityRef(1, 1);
    grid.Insert(entity, V(5, 0));
    var results = new List<EntityRef>();

    grid.QueryRadius(V(0, 0), FP64.FromInt(5), results);

    results.Should().Contain(entity);
  }

  [Fact]
  public void QueryRadius_HandlesNegativeCoordinatesAcrossOrigin() {
    var grid = new SpatialHashGrid(CellSize);
    var negativeEntity = new EntityRef(1, 1);
    var positiveEntity = new EntityRef(2, 1);
    grid.Insert(negativeEntity, V(-1, -1));
    grid.Insert(positiveEntity, V(1, 1));
    var results = new List<EntityRef>();

    grid.QueryRadius(V(0, 0), FP64.FromInt(2), results);

    results.Should().BeEquivalentTo(new[] { negativeEntity, positiveEntity });
  }

  [Fact]
  public void QueryRadius_WithNonPositiveRadius_ReturnsNoResults() {
    var grid = new SpatialHashGrid(CellSize);
    grid.Insert(new EntityRef(1, 1), V(0, 0));
    var results = new List<EntityRef>();

    grid.QueryRadius(V(0, 0), FP64.Zero, results);

    results.Should().BeEmpty();
  }

  [Fact]
  public void QueryRadius_ReturnsMultipleEntitiesStackedAtSamePosition() {
    var grid = new SpatialHashGrid(CellSize);
    var a = new EntityRef(1, 1);
    var b = new EntityRef(2, 1);
    grid.Insert(a, V(0, 0));
    grid.Insert(b, V(0, 0));
    var results = new List<EntityRef>();

    grid.QueryRadius(V(0, 0), FP64.FromInt(1), results);

    results.Should().BeEquivalentTo(new[] { a, b });
  }

  [Fact]
  public void Clear_RemovesPreviouslyInsertedEntities() {
    var grid = new SpatialHashGrid(CellSize);
    grid.Insert(new EntityRef(1, 1), V(0, 0));

    grid.Clear();
    var results = new List<EntityRef>();
    grid.QueryRadius(V(0, 0), FP64.FromInt(10), results);

    results.Should().BeEmpty();
  }

  [Fact]
  public void Clear_ThenReinsert_OnlyReturnsCurrentTickEntities() {
    var grid = new SpatialHashGrid(CellSize);
    grid.Insert(new EntityRef(1, 1), V(0, 0));
    grid.Clear();

    var freshEntity = new EntityRef(2, 1);
    grid.Insert(freshEntity, V(0, 0));
    var results = new List<EntityRef>();
    grid.QueryRadius(V(0, 0), FP64.FromInt(10), results);

    results.Should().ContainSingle().Which.Should().Be(freshEntity);
  }

  [Fact]
  public void QueryRadius_SpanningManyCells_FindsAllEntitiesWithinRange() {
    var grid = new SpatialHashGrid(CellSize);
    var near = new EntityRef(1, 1);
    var far = new EntityRef(2, 1);
    var outOfRange = new EntityRef(3, 1);
    grid.Insert(near, V(2, 0));
    grid.Insert(far, V(18, 0));
    grid.Insert(outOfRange, V(25, 0));
    var results = new List<EntityRef>();

    grid.QueryRadius(V(0, 0), FP64.FromInt(20), results);

    results.Should().BeEquivalentTo(new[] { near, far });
  }
}
