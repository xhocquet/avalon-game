using xpTURN.Klotho.Core;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Commands;

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
