using xpTURN.Klotho.Core;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Commands;

[KlothoSerializable(103)]
public partial class AttackCommand : CommandBase {
  private int[] _sourceUnitIds = new int[8];

  public int TargetUnitId;
  public override bool IsContinuousInput => false;

  public int SourceUnitIdCount { get; private set; }

  public void AddSourceUnitId(int unitId) {
    if (SourceUnitIdCount == _sourceUnitIds.Length) {
      var grown = new int[_sourceUnitIds.Length * 2];
      _sourceUnitIds.CopyTo(grown, 0);
      _sourceUnitIds = grown;
    }

    _sourceUnitIds[SourceUnitIdCount++] = unitId;
  }

  public int GetSourceUnitId(int index) {
    return _sourceUnitIds[index];
  }

  // 12 header + 4 target unit id + 2 source count + 4 per source id
  public override int GetSerializedSize() {
    return 18 + SourceUnitIdCount * 4;
  }

  protected override void SerializeData(ref SpanWriter writer) {
    writer.WriteInt32(TargetUnitId);
    writer.WriteInt16((short)SourceUnitIdCount);
    for (var i = 0; i < SourceUnitIdCount; i++)
      writer.WriteInt32(_sourceUnitIds[i]);
  }

  protected override void DeserializeData(ref SpanReader reader) {
    TargetUnitId = reader.ReadInt32();
    SourceUnitIdCount = reader.ReadInt16();
    if (_sourceUnitIds.Length < SourceUnitIdCount)
      _sourceUnitIds = new int[SourceUnitIdCount];
    for (var i = 0; i < SourceUnitIdCount; i++)
      _sourceUnitIds[i] = reader.ReadInt32();
  }
}
