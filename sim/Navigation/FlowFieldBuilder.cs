using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Deterministic.Navigation;

namespace Meesles.Avalon.Sim.Navigation;

// Reuses Dijkstra buffers and goal-independent edge costs.
public class FlowFieldBuilder {
  private readonly bool[] _closed;

  private readonly FP64[] _cost;

  // Traversal cost for each triangle edge, indexed tri * 3 + edge.
  private readonly FP64[] _edgeCosts;
  private readonly MinHeap _heap;
  private readonly FPNavMesh _navMesh;
  private readonly int _triCount;

  public FlowFieldBuilder(FPNavMesh navMesh) {
    _navMesh = navMesh;
    _triCount = navMesh.Triangles.Length;
    _cost = new FP64[_triCount];
    _closed = new bool[_triCount];
    _heap = new MinHeap(_triCount * 3 + 1); // one push per directed edge, plus the goal
    _edgeCosts = new FP64[_triCount * 3];

    for (var tri = 0; tri < _triCount; tri++) {
      ref readonly var triangle = ref navMesh.Triangles[tri];
      for (var edge = 0; edge < 3; edge++) {
        var neighborIdx = triangle.GetNeighbor(edge);
        if (neighborIdx < 0)
          continue;

        ref readonly var neighborTri = ref navMesh.Triangles[neighborIdx];
        var delta = neighborTri.centerXZ - triangle.centerXZ;
        _edgeCosts[tri * 3 + edge] = delta.magnitude * neighborTri.costMultiplier;
      }
    }
  }

  public TriangleFlowField Build(int goalTriangleIndex) {
    var nextTriangles = new int[_triCount];

    for (var i = 0; i < _triCount; i++) {
      nextTriangles[i] = TriangleFlowField.Unreachable;
      _cost[i] = FP64.MaxValue;
      _closed[i] = false;
    }

    nextTriangles[goalTriangleIndex] = TriangleFlowField.AtGoal;
    _cost[goalTriangleIndex] = FP64.Zero;

    // Triangle index breaks equal-cost ties.
    _heap.Clear();
    _heap.Push(FP64.Zero, goalTriangleIndex);

    while (_heap.TryPop(out var current, out var poppedCost)) {
      if (_closed[current] || poppedCost != _cost[current]) // stale entry left by a later relaxation
        continue;

      _closed[current] = true;

      ref readonly var currentTri = ref _navMesh.Triangles[current];

      for (var edge = 0; edge < 3; edge++) {
        var neighborIdx = currentTri.GetNeighbor(edge);
        if (neighborIdx < 0)
          continue;

        ref readonly var neighborTri = ref _navMesh.Triangles[neighborIdx];
        if (neighborTri.isBlocked)
          continue;

        var newCost = _cost[current] + _edgeCosts[current * 3 + edge];
        if (newCost >= _cost[neighborIdx])
          continue;

        _cost[neighborIdx] = newCost;
        nextTriangles[neighborIdx] = current;
        _heap.Push(newCost, neighborIdx);
      }
    }

    return new TriangleFlowField(_navMesh, nextTriangles);
  }

  // Relaxed triangles are pushed again; stale entries are skipped on pop.
  private class MinHeap(int capacity) {
    private readonly FP64[] _costs = new FP64[capacity];
    private readonly int[] _triangles = new int[capacity];
    private int _count;

    public void Clear() => _count = 0;

    public void Push(FP64 cost, int triangle) {
      var i = _count++;
      _costs[i] = cost;
      _triangles[i] = triangle;

      while (i > 0) {
        var parent = (i - 1) / 2;
        if (!IsLess(i, parent))
          break;

        Swap(i, parent);
        i = parent;
      }
    }

    public bool TryPop(out int triangle, out FP64 cost) {
      if (_count == 0) {
        triangle = -1;
        cost = FP64.Zero;
        return false;
      }

      triangle = _triangles[0];
      cost = _costs[0];

      _count--;
      _costs[0] = _costs[_count];
      _triangles[0] = _triangles[_count];

      var i = 0;
      while (true) {
        var left = i * 2 + 1;
        if (left >= _count)
          break;

        var smallest = IsLess(left, i) ? left : i;
        var right = left + 1;
        if (right < _count && IsLess(right, smallest))
          smallest = right;

        if (smallest == i)
          break;

        Swap(i, smallest);
        i = smallest;
      }

      return true;
    }

    // Triangle index breaks cost ties, so the pop order never depends on insertion order.
    private bool IsLess(int a, int b) =>
      _costs[a] != _costs[b] ? _costs[a] < _costs[b] : _triangles[a] < _triangles[b];

    private void Swap(int a, int b) {
      (_costs[a], _costs[b]) = (_costs[b], _costs[a]);
      (_triangles[a], _triangles[b]) = (_triangles[b], _triangles[a]);
    }
  }
}
