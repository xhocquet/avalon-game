using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Commands;

// The selection payload shared by every unit order (move, attack, ...): a growable, count-prefixed
// list of stable Unit.UnitId values. Composed into commands rather than inherited so an order can
// carry it alongside whatever else it needs (a ground point, a target unit, ...).
public sealed class UnitIdList {
  private int[] _ids = new int[8];

  public int Count { get; private set; }

  // 2 count + 4 per id
  public int SerializedSize => 2 + Count * 4;

  public int this[int index] => _ids[index];

  public void Add(int unitId) {
    if (Count == _ids.Length) {
      var grown = new int[_ids.Length * 2];
      _ids.CopyTo(grown, 0);
      _ids = grown;
    }

    _ids[Count++] = unitId;
  }

  public void Clear() {
    Count = 0;
  }

  public void Serialize(ref SpanWriter writer) {
    writer.WriteInt16((short)Count);
    for (var i = 0; i < Count; i++)
      writer.WriteInt32(_ids[i]);
  }

  public void Deserialize(ref SpanReader reader) {
    Count = reader.ReadInt16();
    if (_ids.Length < Count)
      _ids = new int[Count];
    for (var i = 0; i < Count; i++)
      _ids[i] = reader.ReadInt32();
  }
}
