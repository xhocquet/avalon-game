using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon;

public class InventorySystem : ISystem {
  public void Update(ref Frame frame) {
    var matchRules = frame.AssetRegistry.Get<MatchRulesAsset>();
    if (matchRules.GoldTickIntervalMs <= 0)
      return;

    var filter = frame.Filter<Inventory, Stats>();
    while (filter.Next(out var entity)) {
      var goldPerTick = frame.GetReadOnly<Stats>(entity).GoldPerTick;
      ref var inventory = ref frame.Get<Inventory>(entity);

      inventory.GoldAccrualRemainderMs += frame.DeltaTimeMs;
      while (inventory.GoldAccrualRemainderMs >= matchRules.GoldTickIntervalMs) {
        inventory.GoldAccrualRemainderMs -= matchRules.GoldTickIntervalMs;
        inventory.Gold += goldPerTick;
      }
    }
  }
}
