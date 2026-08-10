using System.Collections.Generic;
using xpTURN.Klotho.Deterministic.Navigation;

namespace Meesles.Avalon.Sim.Navigation;

public class FlowFieldCache(FPNavMesh navMesh) {
  // A field costs triCount * 21B, so an uncapped cache tops out at triCount² - it grows with the map
  // rather than with what is being played. 64 live goals is well past what a match reaches: a
  // 3000-tick run with a move order every 20 ticks touched 97 distinct goal triangles in total, only
  // a handful of them in use at any one moment.
  private const int Capacity = 64;

  private readonly FlowFieldBuilder _builder = new(navMesh);
  private readonly Dictionary<int, Entry> _fields = new();
  private int _clock;

  public int Version { get; private set; }

  public int Count => _fields.Count;

  public TriangleFlowField GetOrCreate(int goalTriangleIndex) {
    if (_fields.TryGetValue(goalTriangleIndex, out var entry)) {
      entry.LastUsed = ++_clock;
      _fields[goalTriangleIndex] = entry;
      return entry.Field;
    }

    if (_fields.Count >= Capacity)
      EvictLeastRecentlyUsed();

    var field = _builder.Build(goalTriangleIndex);
    _fields[goalTriangleIndex] = new Entry { Field = field, LastUsed = ++_clock };
    return field;
  }

  public void Invalidate() {
    Version++;
    _fields.Clear();
  }

  // Eviction only decides how often a field is rebuilt; Build is a pure function of the navmesh and
  // the goal, so which entry goes is invisible to the simulation.
  private void EvictLeastRecentlyUsed() {
    var oldestKey = 0;
    var oldestUse = int.MaxValue;

    foreach (var pair in _fields)
      if (pair.Value.LastUsed < oldestUse) {
        oldestUse = pair.Value.LastUsed;
        oldestKey = pair.Key;
      }

    _fields.Remove(oldestKey);
  }

  private struct Entry {
    public TriangleFlowField Field;
    public int LastUsed;
  }
}
