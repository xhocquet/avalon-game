using xpTURN.Klotho.Core;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Commands;

[KlothoSerializable(104)]
public partial class SelectFactionCommand : CommandBase {
  public int FactionId;
  public override bool IsContinuousInput => false;

  // 12 header + 4 faction id
  public override int GetSerializedSize() {
    return 16;
  }

  protected override void SerializeData(ref SpanWriter writer) {
    writer.WriteInt32(FactionId);
  }

  protected override void DeserializeData(ref SpanReader reader) {
    FactionId = reader.ReadInt32();
  }
}
