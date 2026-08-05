using xpTURN.Klotho.Core;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Commands;

// Cast a learned slot. No target field: every skill is self-cast while the effects are stubbed. A
// targeted skill adds TargetUnitId or a ground point here, which changes the size and validation but
// not the shape.
[KlothoSerializable(108)]
public partial class CastSkillCommand : CommandBase {
  public int Slot;
  public override bool IsContinuousInput => false;

  // 12 header + 4 slot
  public override int GetSerializedSize() {
    return 16;
  }

  protected override void SerializeData(ref SpanWriter writer) {
    writer.WriteInt32(Slot);
  }

  protected override void DeserializeData(ref SpanReader reader) {
    Slot = reader.ReadInt32();
  }
}
