namespace Meesles.Avalon.Sim.Assets;

// Maps a PickupTypeAsset id to its wallet slot in ResourcesComponent. The slot is the type's offset
// within the AssetIds.PickupType* block, so that block is index-significant: it starts at
// PickupTypeBase, and a deleted type leaves its hole behind rather than shifting the ones after it.
public static class PickupTypes {
  public const int MaxTypes = 8;

  public const int InvalidSlot = -1;

  public static int SlotOf(int typeAssetId) {
    var slot = typeAssetId - AssetIds.PickupTypeBase;
    return slot >= 0 && slot < MaxTypes ? slot : InvalidSlot;
  }

  public static int AssetIdOf(int slot) {
    return AssetIds.PickupTypeBase + slot;
  }
}
