using xpTURN.Klotho.Core;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Commands {
  [KlothoSerializable(103)]
  public partial class AttackCommand : CommandBase {
    public override bool IsContinuousInput => false;

    public int TargetUnitId;

    private int _sourceUnitIdCount;
    private int[] _sourceUnitIds = new int[8];

    public int SourceUnitIdCount => _sourceUnitIdCount;

    public void AddSourceUnitId(int unitId) {
      if (_sourceUnitIdCount == _sourceUnitIds.Length) {
        var grown = new int[_sourceUnitIds.Length * 2];
        _sourceUnitIds.CopyTo(grown, 0);
        _sourceUnitIds = grown;
      }

      _sourceUnitIds[_sourceUnitIdCount++] = unitId;
    }

    public int GetSourceUnitId(int index) => _sourceUnitIds[index];

    // 12 header + 4 target unit id + 2 source count + 4 per source id
    public override int GetSerializedSize() => 18 + _sourceUnitIdCount * 4;

    protected override void SerializeData(ref SpanWriter writer) {
      writer.WriteInt32(TargetUnitId);
      writer.WriteInt16((short)_sourceUnitIdCount);
      for (int i = 0; i < _sourceUnitIdCount; i++)
        writer.WriteInt32(_sourceUnitIds[i]);
    }

    protected override void DeserializeData(ref SpanReader reader) {
      TargetUnitId = reader.ReadInt32();
      _sourceUnitIdCount = reader.ReadInt16();
      if (_sourceUnitIds.Length < _sourceUnitIdCount)
        _sourceUnitIds = new int[_sourceUnitIdCount];
      for (int i = 0; i < _sourceUnitIdCount; i++)
        _sourceUnitIds[i] = reader.ReadInt32();
    }
  }
}
