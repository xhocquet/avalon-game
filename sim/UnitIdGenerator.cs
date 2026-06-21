using Meesles.Avalon.Sim.Models;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim {
  public static class UnitIdGenerator {
    public const int FirstUnitId = 1;

    public static void Initialize(ref Frame frame, int nextUnitId = FirstUnitId) {
      if (frame.TryGetSingleton<UnitIdCounter>(out _)) return;

      var entity = frame.CreateEntity();
      frame.Add(entity, new UnitIdCounter { NextUnitId = nextUnitId });
    }

    public static int Next(ref Frame frame) {
      Initialize(ref frame);

      ref var state = ref frame.GetSingleton<UnitIdCounter>();
      int unitId = state.NextUnitId;
      state.NextUnitId += 1;
      return unitId;
    }
  }
}
