using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// Per-hero gold wallet + owned-item ledger. Gold accrues over time (see InventorySystem); the
// item ledger is an append-only list of purchased ShopItemAsset ids, written by the shop purchase
// handler. Collected resources live in ResourcesComponent, tallied per pickup type. Stored as a
// fixed buffer (not a List) because components must be unmanaged, blittable structs - the whole
// struct is snapshotted via a raw heap memcpy for rollback, and the generated codec walks the fixed
// buffer for hashing/serialization (see HFSMState for the same pattern). Buffer size keeps the
// struct under the 128-byte component ceiling: 4 ints + MaxItems*4 = 112B.
[KlothoComponent(ComponentIds.Inventory)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public unsafe partial struct InventoryComponent : IComponent {
  // Repeatable buys stack, so each purchase appends another entry - this caps how many a hero may own.
  public const int MaxItems = 24;

  public int Gold;
  public int GoldAccrualRemainderMs;
  public int GoldPerTick; // Seeded from MatchRulesAsset

  public int ItemCount;
  public fixed int ItemAssetIds[MaxItems];

  public readonly bool IsItemsFull => ItemCount >= MaxItems;

  // Append a purchased item's asset id to the ledger. Returns false (no-op) when the ledger is full,
  // letting the caller reject the purchase before spending gold.
  public bool TryAddItem(int itemAssetId) {
    if (ItemCount >= MaxItems)
      return false;

    ItemAssetIds[ItemCount] = itemAssetId;
    ItemCount++;
    return true;
  }

  public readonly int GetItemAssetId(int index) {
    return ItemAssetIds[index];
  }

  // How many copies of a given item this hero owns (buffs stack, so callers may care about counts).
  public readonly int CountOf(int itemAssetId) {
    var count = 0;
    for (var i = 0; i < ItemCount; i++)
      if (ItemAssetIds[i] == itemAssetId)
        count++;

    return count;
  }
}
