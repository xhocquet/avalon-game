using System;
using System.Collections.Generic;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim {
  /// <summary>
  /// Deterministic uniform spatial hash over the XZ plane. Rebuilt once per tick (Clear + Insert)
  /// and queried as a broad-phase for proximity lookups (target acquisition, avoidance, etc.) that
  /// would otherwise scan every entity. Cell lookups are keyed by exact coordinates rather than
  /// dictionary enumeration, so results stay deterministic regardless of hash iteration order.
  /// </summary>
  public sealed class SpatialHashGrid {
    private readonly FP64 _inverseCellSize;
    private readonly Dictionary<(int x, int z), List<(EntityRef Entity, FPVector2 Position)>> _cells = new();

    public SpatialHashGrid(FP64 cellSize) {
      if (cellSize <= FP64.Zero)
        throw new ArgumentOutOfRangeException(nameof(cellSize), "Cell size must be positive.");

      _inverseCellSize = FP64.One / cellSize;
    }

    /// <summary>Empties the grid. Call once per tick before re-inserting entities.</summary>
    public void Clear() {
      foreach (var cell in _cells.Values)
        ListPool<(EntityRef Entity, FPVector2 Position)>.Return(cell);

      _cells.Clear();
    }

    /// <summary>Inserts an entity at its XZ position for later proximity queries.</summary>
    public void Insert(EntityRef entity, FPVector2 positionXZ) {
      var key = CellKey(positionXZ);
      if (!_cells.TryGetValue(key, out var cell)) {
        cell = ListPool<(EntityRef Entity, FPVector2 Position)>.Get();
        _cells[key] = cell;
      }

      cell.Add((entity, positionXZ));
    }

    /// <summary>
    /// Fills <paramref name="results"/> with every inserted entity within <paramref name="radius"/>
    /// (inclusive) of <paramref name="center"/>, exact-distance filtered. Does not exclude the
    /// query origin itself if it was also inserted at/within range of <paramref name="center"/>.
    /// </summary>
    public void QueryRadius(FPVector2 center, FP64 radius, List<EntityRef> results) {
      results.Clear();
      if (radius <= FP64.Zero)
        return;

      FP64 radiusSq = radius * radius;
      int minX = CellCoord(center.x - radius);
      int maxX = CellCoord(center.x + radius);
      int minZ = CellCoord(center.y - radius);
      int maxZ = CellCoord(center.y + radius);

      for (int x = minX; x <= maxX; x++) {
        for (int z = minZ; z <= maxZ; z++) {
          if (!_cells.TryGetValue((x, z), out var cell))
            continue;

          for (int i = 0; i < cell.Count; i++) {
            FPVector2 delta = cell[i].Position - center;
            if (delta.sqrMagnitude <= radiusSq)
              results.Add(cell[i].Entity);
          }
        }
      }
    }

    private (int x, int z) CellKey(FPVector2 position) => (CellCoord(position.x), CellCoord(position.y));

    private int CellCoord(FP64 value) => FP64.Floor(value * _inverseCellSize).ToInt();
  }
}
