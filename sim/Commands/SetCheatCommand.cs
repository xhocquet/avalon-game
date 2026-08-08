using xpTURN.Klotho.Core;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Commands;

// Turns a CheatFlags bitmask on or off for the issuing player. See Cheats for the scope and the
// deliberate absence of any authority gate.
[KlothoSerializable(109)]
public partial class SetCheatCommand : CommandBase {
  public int Flags;
  public int Enabled; // 0 = clear, 1 = set
  public override bool IsContinuousInput => false;

  // 12 header + 4 flags + 4 enabled
  public override int GetSerializedSize() {
    return 20;
  }

  protected override void SerializeData(ref SpanWriter writer) {
    writer.WriteInt32(Flags);
    writer.WriteInt32(Enabled);
  }

  protected override void DeserializeData(ref SpanReader reader) {
    Flags = reader.ReadInt32();
    Enabled = reader.ReadInt32();
  }
}
