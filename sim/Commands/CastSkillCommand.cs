using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Commands;

[KlothoSerializable(108)]
public partial class CastSkillCommand : CommandBase {
  public int Slot;
  public FP64 TargetX;
  public FP64 TargetZ;
  public override bool IsContinuousInput => false;

  public override int GetSerializedSize() {
    // slot + TargetX, TargetZ
    return CommandLimits.HeaderBytes + CommandLimits.Int32Bytes + CommandLimits.Fp64Bytes * 2;
  }

  protected override void SerializeData(ref SpanWriter writer) {
    writer.WriteInt32(Slot);
    writer.WriteFP(TargetX);
    writer.WriteFP(TargetZ);
  }

  protected override void DeserializeData(ref SpanReader reader) {
    Slot = reader.ReadInt32();
    TargetX = reader.ReadFP64();
    TargetZ = reader.ReadFP64();
  }
}
