using xpTURN.Klotho.Core;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Commands;

[KlothoSerializable(106)]
public partial class PurchaseItemCommand : CommandBase {
  public int ItemAssetId;
  public override bool IsContinuousInput => false;

  public override int GetSerializedSize() {
    return CommandLimits.HeaderBytes + CommandLimits.Int32Bytes; // item asset id
  }

  protected override void SerializeData(ref SpanWriter writer) {
    writer.WriteInt32(ItemAssetId);
  }

  protected override void DeserializeData(ref SpanReader reader) {
    ItemAssetId = reader.ReadInt32();
  }
}
