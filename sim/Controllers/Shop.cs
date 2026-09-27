using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

public static class ShopActions {
  public static bool TryPurchase(ref Frame frame, int playerId, int itemAssetId) {
    var block = EvaluatePurchase(ref frame, playerId, itemAssetId, out var heroEntity, out var item);
    if (block != PurchaseRejectedReasons.None) {
      Reject(ref frame, playerId, itemAssetId, Describe(ref frame, block, heroEntity, item));
      return false;
    }

    ref var inventory = ref frame.Get<Inventory>(heroEntity);
    inventory.Gold -= CostFor(ref frame, playerId, item);
    inventory.TryAddItem(itemAssetId);
    ref var stats = ref frame.Get<Stats>(heroEntity);
    stats.Add(StatType.AttackDamage, item.AttackBonus);

    SimLog.Info(ref frame,
      $"[Shop] ACCEPT tick={frame.Tick} playerId={playerId} itemId={itemAssetId} cost={item.Cost} +ad={item.AttackBonus} goldLeft={inventory.Gold} attackDamageNow={stats.AttackDamage} items={inventory.ItemCount}");
    return true;
  }

  // Would TryPurchase accept this buy right now? The client asks before it queues a command, so a buy
  // already known to fail never reaches the wire and the sim never has to reject it.
  // Read-only and allocation-free: safe to call every frame off the predicted frame.
  public static bool CanPurchase(ref Frame frame, int playerId, int itemAssetId) {
    return EvaluatePurchase(ref frame, playerId, itemAssetId, out _, out _) == PurchaseRejectedReasons.None;
  }

  // CanPurchase asked as if pendingGold were already spent and pendingItems already in the ledger -
  // buys the client has queued but the predicted frame has not run yet. Klotho schedules local input
  // InputDelayTicks ahead, so without this the client would re-approve a buy it has already spent the
  // gold on and the sim would reject the second command on arrival.
  public static bool CanPurchase(ref Frame frame, int playerId, int itemAssetId, int pendingGold,
    int pendingItems) {
    return EvaluatePurchase(ref frame, playerId, itemAssetId, out _, out _, pendingGold, pendingItems)
           == PurchaseRejectedReasons.None;
  }

  // The purchase rules, in one place. TryPurchase turns a block into a reject log; the client turns it
  // into a greyed button. Nothing here mutates the frame.
  private static PurchaseRejectedReasons EvaluatePurchase(ref Frame frame, int playerId, int itemAssetId,
    out EntityRef heroEntity, out ShopItemAsset item, int pendingGold = 0, int pendingItems = 0) {
    item = null;

    if (!UnitLookup.TryGetPlayerHero(ref frame, playerId, out heroEntity))
      return PurchaseRejectedReasons.NoHero;

    if (!frame.AssetRegistry.TryGet<ShopItemAsset>(itemAssetId, out item))
      return PurchaseRejectedReasons.ItemAssetMissing;

    if (!frame.Has<Inventory>(heroEntity) || !frame.Has<Stats>(heroEntity))
      return PurchaseRejectedReasons.HeroMissingInventoryOrStats;

    ref readonly var inventory = ref frame.GetReadOnly<Inventory>(heroEntity);
    if (inventory.Gold - pendingGold < CostFor(ref frame, playerId, item))
      return PurchaseRejectedReasons.InsufficientGold;

    if (!IsHeroNearTeamShop(ref frame, heroEntity))
      return PurchaseRejectedReasons.OutOfRange;

    return inventory.ItemCount + pendingItems >= Inventory.MaxItems
      ? PurchaseRejectedReasons.InventoryFull
      : PurchaseRejectedReasons.None;
  }

  // Block code -> the reason= text. Only walked on the reject path, so the diagnostic detail costs
  // nothing on the predicate path the client polls.
  private static string Describe(ref Frame frame, PurchaseRejectedReasons block, EntityRef heroEntity,
    ShopItemAsset item) {
    switch (block) {
      case PurchaseRejectedReasons.NoHero: return "no_hero_for_player";
      case PurchaseRejectedReasons.ItemAssetMissing: return "item_asset_missing";
      case PurchaseRejectedReasons.HeroMissingInventoryOrStats:
        return
          $"hero_missing_inventory_or_stats hasInv={frame.Has<Inventory>(heroEntity)} hasStats={frame.Has<Stats>(heroEntity)}";
      case PurchaseRejectedReasons.OutOfRange: return "out_of_range";
    }

    ref readonly var inventory = ref frame.GetReadOnly<Inventory>(heroEntity);
    return block switch {
      PurchaseRejectedReasons.InsufficientGold => $"insufficient_gold gold={inventory.Gold} cost={item.Cost}",
      PurchaseRejectedReasons.InventoryFull => $"inventory_full itemCount={inventory.ItemCount}",
      _ => block.ToString()
    };
  }

  // Shop access is a team question: a hero buys at the Shop marker its own team owns.
  public static bool IsHeroNearTeamShop(ref Frame frame, EntityRef heroEntity) {
    if (!frame.Has<Team>(heroEntity) || !frame.Has<TransformComponent>(heroEntity))
      return false;

    if (HasFreeShop(ref frame, heroEntity))
      return true;

    var teamId = frame.GetReadOnly<Team>(heroEntity).TeamId;
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

  // FreeShop zeroes the price rather than skipping the gold write, so the buy still runs the one
  // subtraction and the ledger the HUD paints from stays the only source of the number.
  private static int CostFor(ref Frame frame, int playerId, ShopItemAsset item) {
    return Cheats.IsEnabled(ref frame, playerId, CheatFlags.FreeShop) ? 0 : item.Cost;
  }

  private static bool HasFreeShop(ref Frame frame, EntityRef heroEntity) {
    return frame.Has<Hero>(heroEntity) &&
           Cheats.IsEnabled(ref frame, frame.GetReadOnly<Hero>(heroEntity).PlayerId, CheatFlags.FreeShop);
  }

  private static void Reject(ref Frame frame, int playerId, int itemAssetId, string reason) {
    SimLog.Info(ref frame,
      $"[Shop] REJECT tick={frame.Tick} playerId={playerId} itemId={itemAssetId} reason={reason}");
  }
}
