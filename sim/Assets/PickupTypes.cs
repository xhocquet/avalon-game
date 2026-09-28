namespace Meesles.Avalon.Sim.Assets;

// Maps pickup asset ids to Resources slots by offset from PickupTypeBase.
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
