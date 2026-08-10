using xpTURN.Klotho.Core;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Commands;

[KlothoSerializable(104)]
public partial class SelectFactionCommand : CommandBase {
  public int FactionId;
  public override bool IsContinuousInput => false;

  public override int GetSerializedSize() {
    return CommandLimits.HeaderBytes + CommandLimits.Int32Bytes; // faction id
  }

  protected override void SerializeData(ref SpanWriter writer) {
    writer.WriteInt32(FactionId);
  }

  protected override void DeserializeData(ref SpanReader reader) {
    FactionId = reader.ReadInt32();
  }
}
