using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Commands {
  [KlothoSerializable(100)]
  public partial class MoveCommand : CommandBase {
    public override bool IsContinuousInput => false;

    public FP64 TargetX;
    public FP64 TargetZ;

    private int _unitIdCount;
    private int[] _unitIds = new int[8];

    public int UnitIdCount => _unitIdCount;

    public void ClearUnitIds() => _unitIdCount = 0;

    public void AddUnitId(int unitId) {
      if (_unitIdCount == _unitIds.Length) {
        var grown = new int[_unitIds.Length * 2];
        _unitIds.CopyTo(grown, 0);
        _unitIds = grown;
      }
      _unitIds[_unitIdCount++] = unitId;
    }

    public int GetUnitId(int index) => _unitIds[index];

    // 12 header + 8 TargetX + 8 TargetZ + 2 count + 4 per id
    public override int GetSerializedSize() => 30 + _unitIdCount * 4;

    protected override void SerializeData(ref SpanWriter writer) {
      writer.WriteFP(TargetX);
      writer.WriteFP(TargetZ);
      writer.WriteInt16((short)_unitIdCount);
      for (int i = 0; i < _unitIdCount; i++)
        writer.WriteInt32(_unitIds[i]);
    }

    protected override void DeserializeData(ref SpanReader reader) {
      TargetX = reader.ReadFP64();
      TargetZ = reader.ReadFP64();
      _unitIdCount = reader.ReadInt16();
      if (_unitIds.Length < _unitIdCount)
        _unitIds = new int[_unitIdCount];
      for (int i = 0; i < _unitIdCount; i++)
        _unitIds[i] = reader.ReadInt32();
    }
  }
}
