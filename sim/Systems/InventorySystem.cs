using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon;

public class InventorySystem : ISystem {
  public void Update(ref Frame frame) {
    var matchRules = frame.AssetRegistry.Get<MatchRulesAsset>();
    if (matchRules.GoldTickIntervalMs <= 0)
      return;

    var filter = frame.Filter<InventoryComponent>();
    while (filter.Next(out var entity)) {
      ref var inventory = ref frame.Get<InventoryComponent>(entity);

      inventory.GoldAccrualRemainderMs += frame.DeltaTimeMs;
      while (inventory.GoldAccrualRemainderMs >= matchRules.GoldTickIntervalMs) {
        inventory.GoldAccrualRemainderMs -= matchRules.GoldTickIntervalMs;
        inventory.Gold += inventory.GoldPerTick;
      }
    }
  }
}
