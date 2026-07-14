using xpTURN.Klotho.Core;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Commands;

// Adjusts one of a player's Stats attributes by a delta. The mechanism for changing stats over
// time (items, oasis captures, buffs, etc. will send this once those systems exist).
[KlothoSerializable(105)]
public partial class ModifyStatCommand : CommandBase {
  public StatType StatType;
  public int Delta;
  public override bool IsContinuousInput => false;

  // 12 header + 4 stat type + 4 delta
  public override int GetSerializedSize() {
    return 20;
  }

  protected override void SerializeData(ref SpanWriter writer) {
    writer.WriteInt32((int)StatType);
    writer.WriteInt32(Delta);
  }

  protected override void DeserializeData(ref SpanReader reader) {
    StatType = (StatType)reader.ReadInt32();
    Delta = reader.ReadInt32();
  }
}
