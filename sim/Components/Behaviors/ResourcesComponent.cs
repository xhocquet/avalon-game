using System.Runtime.InteropServices;
using Meesles.Avalon.Sim.Assets;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// Per-hero resource wallet: one tally per pickup type, indexed by PickupTypes.SlotOf. Lives apart
// from InventoryComponent because that struct already sits near the 128-byte component ceiling.
// Fixed buffer for the same reason as InventoryComponent.ItemAssetIds - components must be
// unmanaged and blittable.
[KlothoComponent(ComponentIds.Resources)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public unsafe partial struct ResourcesComponent : IComponent {
  public fixed int Counts[PickupTypes.MaxTypes];

  public readonly int Total {
    get {
      var total = 0;
      for (var i = 0; i < PickupTypes.MaxTypes; i++)
        total += Counts[i];

      return total;
    }
  }

  public readonly int GetSlot(int slot) {
    return slot >= 0 && slot < PickupTypes.MaxTypes ? Counts[slot] : 0;
  }

  public readonly int CountOf(int typeAssetId) {
    return GetSlot(PickupTypes.SlotOf(typeAssetId));
  }

  // No-op for a type id outside the block, so an unauthored pickup can't corrupt a neighbouring slot.
  public void Add(int typeAssetId, int amount) {
    var slot = PickupTypes.SlotOf(typeAssetId);
    if (slot == PickupTypes.InvalidSlot)
      return;

    Counts[slot] += amount;
  }
}
