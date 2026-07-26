using System.Runtime.InteropServices;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// Gold/resource economy: the hero wallet + item ledger, collectable pickups, and the oases that
// eject them. Handled by InventorySystem, PickupSystem, and OasisSpawnSystem.

// Per-hero wallet + owned-item ledger. Gold/Resources accrue over time (see InventorySystem); the
// item ledger is an append-only list of purchased ShopItemAsset ids, written by the shop purchase
// handler. Stored as a fixed buffer (not a List) because components must be unmanaged, blittable
// structs - the whole struct is snapshotted via a raw heap memcpy for rollback, and the generated
// codec walks the fixed buffer for hashing/serialization (see HFSMState for the same pattern).
// Buffer size keeps the struct under the 128-byte component ceiling: 4 ints + MaxItems*4 = 112B.
[KlothoComponent(ComponentIds.Inventory)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public unsafe partial struct Inventory : IComponent {
  // Repeatable buys stack, so each purchase appends another entry - this caps how many a hero may own.
  public const int MaxItems = 24;

  public int Gold;
  public int GoldAccrualRemainderMs;
  public int Resources;

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

// Neutral, collectable resource pickup. Like Oasis, carries no Team/Health/Unit so it stays
// invisible to TargetAcquisitionSystem/DamageSystem; PickupSystem collects it by proximity only.
[KlothoComponent(ComponentIds.Pickup)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct Pickup : IComponent {
  public int PickupId;
  public int Amount;
  // public int Type; // TODO: distinguish resource types once more than one exists
}

[KlothoComponent(ComponentIds.Oasis)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct Oasis : IComponent {
  public int OasisId;
  public int SpawnCooldownRemainingMs;
}

// Temporary indicator of an oasis about to spawn a resource
[KlothoComponent(ComponentIds.OasisEjectPending)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct OasisEjectPending : IComponent {
  public int PickupId;
  public int Amount;
  public FPVector3 TargetPosition;
  public int RemainingMs;
}

// Temporary indicator of a resource flying through the air from an oasis
[KlothoComponent(ComponentIds.OasisResourceLanding)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct OasisResourceLanding : IComponent {
  public int PickupId;
  public int Amount;
  public FPVector3 TargetPosition;
  public int RemainingMs;
}
