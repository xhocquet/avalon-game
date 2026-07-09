using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Commands;

[KlothoSerializable(100)]
public partial class MoveCommand : CommandBase {
  private int[] _unitIds = new int[8];

  public FP64 TargetX;
  public FP64 TargetZ;
  public override bool IsContinuousInput => false;

  public int UnitIdCount { get; private set; }

  public void AddUnitId(int unitId) {
    if (UnitIdCount == _unitIds.Length) {
      var grown = new int[_unitIds.Length * 2];
      _unitIds.CopyTo(grown, 0);
      _unitIds = grown;
    }

    _unitIds[UnitIdCount++] = unitId;
  }

  public int GetUnitId(int index) {
    return _unitIds[index];
  }

  // 12 header + 8 TargetX + 8 TargetZ + 2 count + 4 per id
  public override int GetSerializedSize() {
    return 30 + UnitIdCount * 4;
  }

  protected override void SerializeData(ref SpanWriter writer) {
    writer.WriteFP(TargetX);
    writer.WriteFP(TargetZ);
    writer.WriteInt16((short)UnitIdCount);
    for (var i = 0; i < UnitIdCount; i++)
      writer.WriteInt32(_unitIds[i]);
  }

  protected override void DeserializeData(ref SpanReader reader) {
    TargetX = reader.ReadFP64();
    TargetZ = reader.ReadFP64();
    UnitIdCount = reader.ReadInt16();
    if (_unitIds.Length < UnitIdCount)
      _unitIds = new int[UnitIdCount];
    for (var i = 0; i < UnitIdCount; i++)
      _unitIds[i] = reader.ReadInt32();
  }
}
