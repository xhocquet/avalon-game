using System.Collections.Generic;
using Meesles.Avalon.Sim.Models;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim {
  public sealed class UnitIdIndex {
    private readonly Dictionary<int, EntityRef> _index = new();

    public void Rebuild(ref Frame frame) {
      _index.Clear();
      var filter = frame.Filter<Unit>();
      while (filter.Next(out var entity)) {
        ref readonly var unit = ref frame.GetReadOnly<Unit>(entity);
        _index[unit.UnitId] = entity;
      }
    }

    public bool TryGet(int unitId, out EntityRef entity) {
      return _index.TryGetValue(unitId, out entity);
    }
  }
}
