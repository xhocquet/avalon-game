using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon;

public class InventorySystem : ISystem {
  public void Update(ref Frame frame) {
    var matchRules = frame.AssetRegistry.Get<MatchRulesAsset>();
    if (matchRules.GoldTickIntervalMs <= 0)
      return;

    // Gated on the match clock, not on how long a hero has existed, so a late-spawning hero doesn't
    // get its own private delay.
    if (frame.Tick < GoldStartTick(ref frame, matchRules))
      return;

    var filter = frame.Filter<Inventory>();
    while (filter.Next(out var entity)) {
      ref var inventory = ref frame.Get<Inventory>(entity);

      inventory.GoldAccrualRemainderMs += frame.DeltaTimeMs;
      while (inventory.GoldAccrualRemainderMs >= matchRules.GoldTickIntervalMs) {
        inventory.GoldAccrualRemainderMs -= matchRules.GoldTickIntervalMs;
        inventory.Gold += inventory.GoldPerTick;
      }
    }
  }

  private static int GoldStartTick(ref Frame frame, MatchRulesAsset matchRules) {
    if (matchRules.GoldStartDelayMs <= 0)
      return 0;

    var deltaTimeMs = TickMath.DeltaTimeMs(ref frame);
    return (matchRules.GoldStartDelayMs + deltaTimeMs - 1) / deltaTimeMs; // ceil: pay on/after the delay
  }
}
