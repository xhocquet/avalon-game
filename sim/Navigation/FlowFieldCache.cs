using System.Collections.Generic;
using xpTURN.Klotho.Deterministic.Navigation;

namespace Meesles.Avalon.Sim.Navigation;

public sealed class FlowFieldCache {
  private readonly Dictionary<int, TriangleFlowField> _fields = new();
  private readonly FPNavMesh _navMesh;

  public FlowFieldCache(FPNavMesh navMesh) {
    _navMesh = navMesh;
  }

  public int Version { get; private set; }

  public TriangleFlowField GetOrCreate(int goalTriangleIndex) {
    if (_fields.TryGetValue(goalTriangleIndex, out var field))
      return field;

    field = TriangleFlowField.Compute(_navMesh, goalTriangleIndex);
    _fields[goalTriangleIndex] = field;
    return field;
  }

  public void Invalidate() {
    Version++;
    _fields.Clear();
  }
}
