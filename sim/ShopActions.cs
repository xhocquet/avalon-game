using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Logging;

namespace Meesles.Avalon.Sim;

// The rules behind PurchaseItemCommand. CommandSystem dispatches straight into these so the command
// layer stays a switch and the rules can be exercised without a wire round-trip.
//
// CommandValidation has already checked the payload's shape; everything here is game state — the
// hero exists, the gold is there, the hero is standing at its own shop.
public static class ShopActions {
  // Buy one item for the player's hero: gold out, item into the inventory, its bonus onto Stats.
  public static bool TryPurchase(ref Frame frame, int playerId, int itemAssetId) {
    var block = EvaluatePurchase(ref frame, playerId, itemAssetId, out var heroEntity, out var item);
    if (block != PurchaseBlock.None) {
      Reject(ref frame, playerId, itemAssetId, Describe(ref frame, block, heroEntity, item));
      return false;
    }

    ref var inventory = ref frame.Get<InventoryComponent>(heroEntity);
    inventory.Gold -= item.Cost;
    inventory.TryAddItem(itemAssetId);
    ref var stats = ref frame.Get<StatsComponent>(heroEntity);
    stats.Add(StatType.Strength, item.AttackBonus);

    SimLog.Info(ref frame,
      $"[Shop] ACCEPT tick={frame.Tick} playerId={playerId} itemId={itemAssetId} cost={item.Cost} +str={item.AttackBonus} goldLeft={inventory.Gold} strengthNow={stats.Strength} items={inventory.ItemCount}");
    return true;
  }

  // Would TryPurchase accept this buy right now? The client asks before it queues a command, so a buy
  // already known to fail never reaches the wire and the sim never has to reject it.
  // Read-only and allocation-free: safe to call every frame off the predicted frame.
  public static bool CanPurchase(ref Frame frame, int playerId, int itemAssetId) {
    return EvaluatePurchase(ref frame, playerId, itemAssetId, out _, out _) == PurchaseBlock.None;
  }

  // The purchase rules, in one place. TryPurchase turns a block into a reject log; the client turns it
  // into a greyed button. Nothing here mutates the frame.
  private static PurchaseBlock EvaluatePurchase(ref Frame frame, int playerId, int itemAssetId,
    out EntityRef heroEntity, out ShopItemAsset item) {
    item = null;

    if (!UnitLookup.TryGetPlayerHero(ref frame, playerId, out heroEntity))
      return PurchaseBlock.NoHero;

    if (!frame.AssetRegistry.TryGet<ShopItemAsset>(itemAssetId, out item))
      return PurchaseBlock.ItemAssetMissing;

    if (!frame.Has<InventoryComponent>(heroEntity) || !frame.Has<StatsComponent>(heroEntity))
      return PurchaseBlock.HeroMissingInventoryOrStats;

    ref readonly var inventory = ref frame.GetReadOnly<InventoryComponent>(heroEntity);
    if (inventory.Gold < item.Cost)
      return PurchaseBlock.InsufficientGold;

    if (!IsHeroNearTeamShop(ref frame, heroEntity))
      return PurchaseBlock.OutOfRange;

    return inventory.IsItemsFull ? PurchaseBlock.InventoryFull : PurchaseBlock.None;
  }

  // Block code -> the reason= text. Only walked on the reject path, so the diagnostic detail costs
  // nothing on the predicate path the client polls.
  private static string Describe(ref Frame frame, PurchaseBlock block, EntityRef heroEntity,
    ShopItemAsset item) {
    switch (block) {
      case PurchaseBlock.NoHero: return "no_hero_for_player";
      case PurchaseBlock.ItemAssetMissing: return "item_asset_missing";
      case PurchaseBlock.HeroMissingInventoryOrStats:
        return
          $"hero_missing_inventory_or_stats hasInv={frame.Has<InventoryComponent>(heroEntity)} hasStats={frame.Has<StatsComponent>(heroEntity)}";
      case PurchaseBlock.OutOfRange: return "out_of_range";
    }

    ref readonly var inventory = ref frame.GetReadOnly<InventoryComponent>(heroEntity);
    return block switch {
      PurchaseBlock.InsufficientGold => $"insufficient_gold gold={inventory.Gold} cost={item.Cost}",
      PurchaseBlock.InventoryFull => $"inventory_full itemCount={inventory.ItemCount}",
      _ => block.ToString()
    };
  }

  // Why a buy cannot go through. A code rather than a string so the client can ask every frame without
  // allocating; Describe renders it only when the sim logs a rejection.
  private enum PurchaseBlock {
    None,
    NoHero,
    ItemAssetMissing,
    HeroMissingInventoryOrStats,
    InsufficientGold,
    OutOfRange,
    InventoryFull
  }

  // Shop access is a team question: a hero buys at the Shop marker its own team owns.
  public static bool IsHeroNearTeamShop(ref Frame frame, EntityRef heroEntity) {
    if (!frame.Has<TeamComponent>(heroEntity) || !frame.Has<TransformComponent>(heroEntity))
      return false;

    var teamId = frame.GetReadOnly<TeamComponent>(heroEntity).TeamId;
    if (!frame.AssetRegistry.TryGet<MapLayoutAsset>(out var layout))
      return false;

    if (!layout.TryGetByTypeAndTeam(MapMarkerType.Shop, teamId, out var shopPos))
      return false;

    if (!frame.AssetRegistry.TryGet<ShopRulesAsset>(out var shopRules))
      return false;

    var heroPos = frame.GetReadOnly<TransformComponent>(heroEntity).Position;
    var delta = heroPos - shopPos;
    delta.y = FP64.Zero;

    var range = shopRules.InteractRange;
    return delta.sqrMagnitude <= range * range;
  }

  private static void Reject(ref Frame frame, int playerId, int itemAssetId, string reason) {
    SimLog.Info(ref frame,
      $"[Shop] REJECT tick={frame.Tick} playerId={playerId} itemId={itemAssetId} reason={reason}");
  }
}
