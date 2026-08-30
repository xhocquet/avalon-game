using xpTURN.Klotho.Core;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Commands;

// Spend one skill point on a slot. Slot is a SkillSlot index; CommandValidation range-checks it before
// SkillActions indexes Skills's fixed buffers with it.
[KlothoSerializable(107)]
public partial class UpgradeSkillCommand : CommandBase {
  public int Slot;
  public override bool IsContinuousInput => false;

  public override int GetSerializedSize() {
    return CommandLimits.HeaderBytes + CommandLimits.Int32Bytes; // slot
  }

  protected override void SerializeData(ref SpanWriter writer) {
    writer.WriteInt32(Slot);
  }

  protected override void DeserializeData(ref SpanReader reader) {
    Slot = reader.ReadInt32();
  }
}
