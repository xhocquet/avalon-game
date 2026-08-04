using xpTURN.Klotho.Core;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Commands;

[KlothoSerializable(103)]
public partial class AttackCommand : CommandBase {
  public int TargetUnitId;
  public override bool IsContinuousInput => false;

  public UnitIdList UnitIds { get; } = new();

  // 12 header + 4 target unit id + unit ids
  public override int GetSerializedSize() {
    return 16 + UnitIds.SerializedSize;
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
