using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Commands;

[KlothoSerializable(100)]
public partial class MoveCommand : CommandBase, IUnitOrderCommand {
  public FP64 TargetX;
  public FP64 TargetZ;
  public override bool IsContinuousInput => false;

  public UnitIdList UnitIds { get; } = new();

  // 12 header + 8 TargetX + 8 TargetZ + unit ids
  public override int GetSerializedSize() {
    return 28 + UnitIds.SerializedSize;
  }

  protected override void SerializeData(ref SpanWriter writer) {
    writer.WriteFP(TargetX);
    writer.WriteFP(TargetZ);
    UnitIds.Serialize(ref writer);
  }

  protected override void DeserializeData(ref SpanReader reader) {
    TargetX = reader.ReadFP64();
    TargetZ = reader.ReadFP64();
    UnitIds.Deserialize(ref reader);
  }
}
