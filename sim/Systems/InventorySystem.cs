using Meesles.Avalon.Sim.Models;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon;

// Owns every player's Inventory: passive resource accrual today, and eventually items/spend
// handling. Ticks in whole milliseconds (not FP64 seconds) so accrual stays exact regardless of
// DeltaTimeMs, instead of accumulating fixed-point rounding error every frame.
public class InventorySystem : ISystem {
  private const int GoldPerTick = 1;
  private const int GoldTickIntervalMs = 1000;

  public void Update(ref Frame frame) {
    var filter = frame.Filter<Inventory>();
    while (filter.Next(out var entity)) {
      ref var inventory = ref frame.Get<Inventory>(entity);

      inventory.GoldAccrualRemainderMs += frame.DeltaTimeMs;
      while (inventory.GoldAccrualRemainderMs >= GoldTickIntervalMs) {
        inventory.GoldAccrualRemainderMs -= GoldTickIntervalMs;
        inventory.Gold += GoldPerTick;
      }
    }
  }
}
