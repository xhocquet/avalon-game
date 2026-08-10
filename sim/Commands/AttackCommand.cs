using xpTURN.Klotho.Core;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Commands;

[KlothoSerializable(103)]
public partial class AttackCommand : CommandBase {
  public int TargetUnitId;
  public override bool IsContinuousInput => false;

  public UnitIdList UnitIds { get; } = new();

  public override int GetSerializedSize() {
    return CommandLimits.HeaderBytes + CommandLimits.Int32Bytes + UnitIds.SerializedSize; // target unit id
  }

  protected override void SerializeData(ref SpanWriter writer) {
    writer.WriteInt32(TargetUnitId);
    UnitIds.Serialize(ref writer);
  }

  protected override void DeserializeData(ref SpanReader reader) {
    TargetUnitId = reader.ReadInt32();
    UnitIds.Deserialize(ref reader);
  }
}
