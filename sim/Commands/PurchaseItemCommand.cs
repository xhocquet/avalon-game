using xpTURN.Klotho.Core;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Commands;

// Buys one shop item for the sending player's hero. CommandSystem re-validates everything
// authoritatively (the hero exists, the item id resolves, the hero can afford the Cost, and the
// hero is within range of its team's Shop marker) before deducting gold and applying the item's
// AttackBonus to the hero's Stats. ItemAssetId is a ShopItemAsset AssetId (300 range).
[KlothoSerializable(106)]
public partial class PurchaseItemCommand : CommandBase {
  public int ItemAssetId;
  public override bool IsContinuousInput => false;

  // 12 header + 4 item asset id
  public override int GetSerializedSize() {
    return 16;
  }

  protected override void SerializeData(ref SpanWriter writer) {
    writer.WriteInt32(ItemAssetId);
  }

  protected override void DeserializeData(ref SpanReader reader) {
    ItemAssetId = reader.ReadInt32();
  }
}
