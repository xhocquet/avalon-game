using System;
using System.Collections.Generic;
using xpTURN.Klotho.Deterministic.Math;

namespace Meesles.Avalon.Sim;

// Immutable map geometry; the sparse index contains edges, not moving units.
public sealed class SightGeometry {
  private readonly FPVector2[] _edges;
  private readonly Dictionary<(int, int), List<int>> _cells = new();
  private readonly Dictionary<int, List<int>> _rows = new();
  private readonly FP64 _cellSize;

  public SightGeometry(FPVector2[] edges, FP64 cellSize) {
    _edges = edges ?? Array.Empty<FPVector2>();
    _cellSize = cellSize > FP64.Zero ? cellSize : FP64.One;
    for (var i = 0; i + 1 < _edges.Length; i += 2) {
      var a = _edges[i];
      var b = _edges[i + 1];
      for (var z = Cell(FP64.Min(a.y, b.y)); z <= Cell(FP64.Max(a.y, b.y)); z++) {
        if (!_rows.TryGetValue(z, out var row))
          _rows[z] = row = new List<int>();
        row.Add(i);
      }
      for (var x = Cell(FP64.Min(a.x, b.x)); x <= Cell(FP64.Max(a.x, b.x)); x++)
        for (var z = Cell(FP64.Min(a.y, b.y)); z <= Cell(FP64.Max(a.y, b.y)); z++) {
          if (!_cells.TryGetValue((x, z), out var list))
            _cells[(x, z)] = list = new List<int>();
          list.Add(i);
        }
    }
  }

  public bool IsWalkable(FPVector2 point) {
    if (_edges.Length == 0) return true;
    if (!_rows.TryGetValue(Cell(point.y), out var row)) return false;
    var inside = false;
    foreach (var i in row) {
      var a = _edges[i];
      var b = _edges[i + 1];
      if ((a.y > point.y) == (b.y > point.y))
        continue;
      if (point.x < FP64.Min(a.x, b.x) ||
          (point.x < FP64.Max(a.x, b.x) &&
           point.x < a.x + (point.y - a.y) * (b.x - a.x) / (b.y - a.y)))
        inside = !inside;
    }
    return inside;
  }

  public bool HasLineOfSight(FPVector2 from, FPVector2 to, FP64 sourceRadius, FP64 targetRadius,
    bool ignoreFirstBoundary = false) {
    var delta = to - from;
    var lengthSq = delta.sqrMagnitude;
    if (lengthSq == FP64.Zero)
      return true;

    var x = Cell(from.x);
    var z = Cell(from.y);
    var endX = Cell(to.x);
    var endZ = Cell(to.y);
    var stepX = delta.x >= FP64.Zero ? 1 : -1;
    var stepZ = delta.y >= FP64.Zero ? 1 : -1;
    var absX = FP64.Abs(delta.x);
    var absZ = FP64.Abs(delta.y);
    var nextX = FP64.Abs(FP64.FromInt(x + (stepX > 0 ? 1 : 0)) * _cellSize - from.x);
    var nextZ = FP64.Abs(FP64.FromInt(z + (stepZ > 0 ? 1 : 0)) * _cellSize - from.y);

    var firstHit = FP64.MaxValue;
    var blockedHit = FP64.MaxValue;
    var sourceRadiusSq = sourceRadius * sourceRadius;
    var targetRadiusSq = targetRadius * targetRadius;
    while (true) {
      if (_cells.TryGetValue((x, z), out var edges))
        for (var i = 0; i < edges.Count; i++) {
          var edge = edges[i];
          if (!TryIntersect(from, delta, _edges[edge], _edges[edge + 1], out var t))
            continue;
          firstHit = FP64.Min(firstHit, t);
          var remaining = FP64.One - t;
          if (t * t * lengthSq <= sourceRadiusSq ||
              remaining * remaining * lengthSq <= targetRadiusSq)
            continue;
          if (!ignoreFirstBoundary)
            return false;
          if (blockedHit != FP64.MaxValue && blockedHit != t)
            return false;
          blockedHit = t;
        }

      if (x == endX && z == endZ)
        return blockedHit == FP64.MaxValue ||
               (ignoreFirstBoundary && blockedHit == firstHit);
      if (z == endZ || (x != endX && nextX * absZ < nextZ * absX)) {
        x += stepX;
        nextX += _cellSize;
      }
      else {
        z += stepZ;
        nextZ += _cellSize;
      }
    }
  }

  private int Cell(FP64 value) {
    var raw = value.RawValue;
    var divisor = _cellSize.RawValue;
    var cell = raw / divisor;
    if (raw < 0 && raw % divisor != 0)
      cell--;
    return (int)cell;
  }
  private static FP64 Cross(FPVector2 a, FPVector2 b) => a.x * b.y - a.y * b.x;

  private static bool TryIntersect(FPVector2 from, FPVector2 delta,
    FPVector2 a, FPVector2 b, out FP64 t) {
    t = FP64.Zero;
    var edge = b - a;
    var denominator = Cross(delta, edge);
    if (denominator == FP64.Zero)
      return false;
    var offset = a - from;
    var alongRay = Cross(offset, edge);
    var alongEdge = Cross(offset, delta);
    if (denominator < FP64.Zero) {
      denominator = -denominator;
      alongRay = -alongRay;
      alongEdge = -alongEdge;
    }
    if (alongRay <= FP64.Zero || alongRay >= denominator ||
        alongEdge < FP64.Zero || alongEdge > denominator)
      return false;
    t = alongRay / denominator;
    return t > FP64.Zero && t < FP64.One;
  }
}
