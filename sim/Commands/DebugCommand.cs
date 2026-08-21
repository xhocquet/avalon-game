using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Commands;

// One-shot playground operation issued by the debug console: switch hero, hand out gold, spawn
// minions. Scoped to the issuing player the same way SetCheatCommand is, and gated by nothing
// beyond that — see DebugActions for the rules and Cheats for why that is deliberate.
[KlothoSerializable(110)]
public partial class DebugCommand : CommandBase {
  public int Action; // DebugAction
  public int Param;
  public int FactionId; // SpawnMinions only; 0 picks one, see DebugActions.ResolveSpawnFaction
  public FP64 TargetX;
  public FP64 TargetZ;
  public override bool IsContinuousInput => false;

  public override int GetSerializedSize() {
    return CommandLimits.HeaderBytes + CommandLimits.Int32Bytes * 3 + CommandLimits.Fp64Bytes * 2;
  }

  protected override void SerializeData(ref SpanWriter writer) {
    writer.WriteInt32(Action);
    writer.WriteInt32(Param);
    writer.WriteInt32(FactionId);
    writer.WriteFP(TargetX);
    writer.WriteFP(TargetZ);
  }

  protected override void DeserializeData(ref SpanReader reader) {
    Action = reader.ReadInt32();
    Param = reader.ReadInt32();
    FactionId = reader.ReadInt32();
    TargetX = reader.ReadFP64();
    TargetZ = reader.ReadFP64();
  }
}
