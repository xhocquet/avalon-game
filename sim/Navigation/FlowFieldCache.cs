using System.Collections.Generic;
using xpTURN.Klotho.Deterministic.Navigation;

namespace Meesles.Avalon.Sim.Navigation {
  public sealed class FlowFieldCache {
    private readonly FPNavMesh _navMesh;
    private readonly Dictionary<int, TriangleFlowField> _fields = new();
    private int _version;

    public FlowFieldCache(FPNavMesh navMesh) {
      _navMesh = navMesh;
    }

    public TriangleFlowField GetOrCreate(int goalTriangleIndex) {
      if (_fields.TryGetValue(goalTriangleIndex, out var field))
        return field;

      field = TriangleFlowField.Compute(_navMesh, goalTriangleIndex);
      _fields[goalTriangleIndex] = field;
      return field;
    }

    public void Invalidate() {
      _version++;
      _fields.Clear();
    }

    public int Version => _version;
  }
}
