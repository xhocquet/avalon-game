using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Commands;

[KlothoSerializable(100)]
public partial class MoveCommand : CommandBase {
  public FP64 TargetX;
  public FP64 TargetZ;
  public override bool IsContinuousInput => false;

  public UnitIdList UnitIds { get; } = new();

  public override int GetSerializedSize() {
    return CommandLimits.HeaderBytes + CommandLimits.Fp64Bytes * 2 + UnitIds.SerializedSize; // TargetX, TargetZ
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
