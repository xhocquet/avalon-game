namespace Meesles.Avalon;

// The shop buys the player has asked for but the simulation has not run yet. The purchase-side twin of
// PredictedSkillState, and it exists for the same reason: Klotho predicts locally but stamps local
// commands at CurrentTick + InputDelayTicks, so the predicted frame keeps reporting the old gold and
// the old inventory for a few ticks after the click - long enough to read as a dropped button. This
// holds the difference so the gold counter, the item panel and the buy buttons all move on the same
// frame as the click, and the sim's result simply takes over when it arrives.
//
// Optimism is bounded the same two ways: an item's outstanding count falls as the frame's own count
// climbs to meet it, and the whole entry expires after ExpiryTicks so a buy the sim rejected reverts
// instead of sticking.
public sealed class PredictedPurchaseState {
  // ~1s at 30Hz. Only reached when a queued command never lands: it has to outlast InputDelayTicks
  // plus any RecommendedExtraDelay escalation, both of which are well under a second.
  private const int ExpiryTicks = 30;

  private static readonly int SlotCount = ShopItemCatalog.ItemDefs.Length;

  // Owned count the frame reported when this item's first outstanding buy was queued. Asked-for count
  // is _baseCount + _asked, and the difference against the frame's current count is what still shows.
  private readonly int[] _baseCount = new int[SlotCount];
  private readonly int[] _asked = new int[SlotCount];
  private readonly int[] _outstanding = new int[SlotCount];
  private readonly int[] _cost = new int[SlotCount];

  // Syncs an item has been waiting with something outstanding. Counted rather than deadlined against
  // frame.Tick so a prediction made before the first sync cannot come out already expired.
  private readonly int[] _waited = new int[SlotCount];

  // Gold committed to queued commands the frame has not deducted yet, and items the frame has not
  // added yet. Both gate the next buy through ShopActions.CanPurchase.
  public int PendingGold { get; private set; }
  public int PendingItems { get; private set; }

  public int OutstandingFor(int itemAssetId) {
    var index = IndexOf(itemAssetId);
    return index >= 0 ? _outstanding[index] : 0;
  }

  // Called when a command is actually queued for sending, never on the click alone - a buy the client
  // itself refused must not move the gold counter.
  public void PredictPurchase(int itemAssetId, int cost) {
    var index = IndexOf(itemAssetId);
    if (index < 0) return;

    _asked[index]++;
    _cost[index] = cost;
    _waited[index] = 0;
    ApplyOutstanding(index, _outstanding[index] + 1);
  }

  // Called once per item per HUD sync with the frame's own owned count, before anything paints.
  public void Observe(int itemAssetId, int simCount) {
    var index = IndexOf(itemAssetId);
    if (index < 0) return;

    if (_asked[index] == 0) {
      // Nothing in flight: the frame is the truth, and its count is the base the next click builds on.
      _baseCount[index] = simCount;
      return;
    }

    if (++_waited[index] >= ExpiryTicks) {
      Retire(index, simCount);
      return;
    }

    // How much of what was asked for the frame still has not shown. Falls to zero as the sim catches
    // up, one item per landed command, and cannot go negative if the frame overshoots.
    var remaining = _baseCount[index] + _asked[index] - simCount;
    if (remaining < 0) remaining = 0;

    ApplyOutstanding(index, remaining);
    if (remaining == 0)
      Retire(index, simCount);
  }

  public void Clear() {
    for (var index = 0; index < SlotCount; index++) {
      _baseCount[index] = 0;
      _asked[index] = 0;
      _outstanding[index] = 0;
      _cost[index] = 0;
      _waited[index] = 0;
    }

    PendingGold = 0;
    PendingItems = 0;
  }

  private void Retire(int index, int simCount) {
    ApplyOutstanding(index, 0);
    _asked[index] = 0;
    _baseCount[index] = simCount;
    _waited[index] = 0;
  }

  // PendingGold and PendingItems track sums over the outstanding counts, so both are maintained
  // through the same door rather than recomputed.
  private void ApplyOutstanding(int index, int value) {
    var delta = value - _outstanding[index];
    _outstanding[index] = value;

    PendingItems += delta;
    if (PendingItems < 0) PendingItems = 0;

    PendingGold += delta * _cost[index];
    if (PendingGold < 0) PendingGold = 0;
  }

  // Catalog order, so the arrays index the same way ActionBarController and InventoryPanelController
  // walk their cells. Linear over six entries - cheaper than a dictionary lookup and allocation-free.
  private static int IndexOf(int itemAssetId) {
    for (var i = 0; i < ShopItemCatalog.ItemDefs.Length; i++)
      if (ShopItemCatalog.ItemDefs[i].Id == itemAssetId)
        return i;

    return -1;
  }
}
