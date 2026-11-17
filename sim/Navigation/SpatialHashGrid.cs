using System;
using System.Collections.Generic;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Navigation;

/// <summary>
///   Deterministic XZ broad-phase. Queries walk cell coordinates, never dictionary order.
/// </summary>
public class SpatialHashGrid {
  private struct Cell {
    public List<(EntityRef Entity, FPVector2 Position, int TeamId)> Entries;
    public int TeamId; // First inserted team.
    public bool MixedTeams;
  }

  private readonly Dictionary<(int x, int z), Cell> _cells = new();
  private readonly FP64 _inverseCellSize;

  public SpatialHashGrid(FP64 cellSize) {
    if (cellSize <= FP64.Zero)
      throw new ArgumentOutOfRangeException(nameof(cellSize), "Cell size must be positive.");

    _inverseCellSize = FP64.One / cellSize;
  }

  /// <summary>Returns cells to the pool.</summary>
  public void Clear() {
    foreach (var cell in _cells.Values)
      ListPool<(EntityRef Entity, FPVector2 Position, int TeamId)>.Return(cell.Entries);

    _cells.Clear();
  }

  /// <summary>Inserts an entity for proximity queries.</summary>
  public void Insert(EntityRef entity, FPVector2 positionXZ, int teamId = 0) {
    var key = CellKey(positionXZ);
    if (!_cells.TryGetValue(key, out var cell)) {
      cell = new Cell {
        Entries = ListPool<(EntityRef Entity, FPVector2 Position, int TeamId)>.Get(),
        TeamId = teamId
      };
      _cells[key] = cell;
    }
    else if (!cell.MixedTeams && cell.TeamId != teamId) {
      cell.MixedTeams = true;
      _cells[key] = cell;
    }

    cell.Entries.Add((entity, positionXZ, teamId));
  }

  /// <summary>
  ///   Fills results with entities at or within radius. Includes the query origin.
  ///   An optional team is excluded before distance checks.
  /// </summary>
  public void QueryRadius(FPVector2 center, FP64 radius, List<EntityRef> results, int? excludedTeam = null) {
    results.Clear();
    if (radius <= FP64.Zero)
      return;

    var radiusSq = radius * radius;
    var minX = CellCoord(center.x - radius);
    var maxX = CellCoord(center.x + radius);
    var minZ = CellCoord(center.y - radius);
    var maxZ = CellCoord(center.y + radius);

    for (var x = minX; x <= maxX; x++)
      for (var z = minZ; z <= maxZ; z++) {
        if (!_cells.TryGetValue((x, z), out var cell))
          continue;
        if (excludedTeam.HasValue && !cell.MixedTeams && cell.TeamId == excludedTeam.Value)
          continue;

        var entries = cell.Entries;
        for (var i = 0; i < entries.Count; i++) {
          if (excludedTeam.HasValue && entries[i].TeamId == excludedTeam.Value)
            continue;
          var delta = entries[i].Position - center;
          if (delta.sqrMagnitude <= radiusSq)
            results.Add(entries[i].Entity);
        }
      }
  }

  private (int x, int z) CellKey(FPVector2 position) {
    return (CellCoord(position.x), CellCoord(position.y));
  }

  private int CellCoord(FP64 value) {
    return FP64.Floor(value * _inverseCellSize).ToInt();
  }
}
