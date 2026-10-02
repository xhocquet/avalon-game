using System.Collections.Generic;
using xpTURN.Klotho.Deterministic.Navigation;

namespace Meesles.Avalon.Sim.Navigation;

public class FlowFieldCache(FPNavMesh navMesh) {
  // Bounds memory by live goals, not map size.
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

  // Eviction only affects rebuild frequency.
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
