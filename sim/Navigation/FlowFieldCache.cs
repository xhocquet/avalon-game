using System.Collections.Generic;
using xpTURN.Klotho.Deterministic.Navigation;

namespace Meesles.Avalon.Sim.Navigation;

public sealed class FlowFieldCache(FPNavMesh navMesh) {
  private readonly Dictionary<int, TriangleFlowField> _fields = new();

  public int Version { get; private set; }

  public TriangleFlowField GetOrCreate(int goalTriangleIndex) {
    if (_fields.TryGetValue(goalTriangleIndex, out var field))
      return field;

    field = TriangleFlowField.Compute(navMesh, goalTriangleIndex);
    _fields[goalTriangleIndex] = field;
    return field;
  }

  public void Invalidate() {
    Version++;
    _fields.Clear();
  }
}
