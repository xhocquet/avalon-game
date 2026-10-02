namespace Meesles.Avalon;

// Locally queued purchases not yet reflected in the sim
public sealed class PredictedPurchaseState {
  private const int ExpiryTicks = 30; // ~1 second at 30 Hz

  private static readonly int SlotCount = ShopItemCatalog.ItemDefs.Length;

  private readonly int[] _baseCount = new int[SlotCount]; // Sim count before outstanding buys
  private readonly int[] _asked = new int[SlotCount];
  private readonly int[] _outstanding = new int[SlotCount];
  private readonly int[] _cost = new int[SlotCount];

  private readonly int[] _waited = new int[SlotCount]; // HUD syncs since the last queued buy

  public int PendingGold { get; private set; }
  public int PendingItems { get; private set; }

  public int OutstandingFor(int itemAssetId) {
    var index = IndexOf(itemAssetId);
    return index >= 0 ? _outstanding[index] : 0;
  }

  public void PredictPurchase(int itemAssetId, int cost) { // Call only after queuing the command
    var index = IndexOf(itemAssetId);
    if (index < 0) return;

    _asked[index]++;
    _cost[index] = cost;
    _waited[index] = 0;
    ApplyOutstanding(index, _outstanding[index] + 1);
  }

  public void Observe(int itemAssetId, int simCount) { // Call each HUD sync before painting
    var index = IndexOf(itemAssetId);
    if (index < 0) return;

    if (_asked[index] == 0) {
      _baseCount[index] = simCount;
      return;
    }

    if (++_waited[index] >= ExpiryTicks) {
      Retire(index, simCount);
      return;
    }

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

  private void ApplyOutstanding(int index, int value) { // Keeps pending totals in sync
    var delta = value - _outstanding[index];
    _outstanding[index] = value;

    PendingItems += delta;
    if (PendingItems < 0) PendingItems = 0;

    PendingGold += delta * _cost[index];
    if (PendingGold < 0) PendingGold = 0;
  }

  private static int IndexOf(int itemAssetId) {
    for (var i = 0; i < ShopItemCatalog.ItemDefs.Length; i++)
      if (ShopItemCatalog.ItemDefs[i].Id == itemAssetId)
        return i;

    return -1;
  }
}
