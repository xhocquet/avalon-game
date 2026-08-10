using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Commands;

// A list of unit IDs used by other commands
public sealed class UnitIdList {
  // Cleared when a deserialized payload declares a count this list will not honour. The order is
  // dropped by CommandValidation rather than executed against a truncated selection.
  public bool IsValid { get; private set; } = true;
  // count + one id each
  public int SerializedSize => CommandLimits.Int16Bytes + Count * CommandLimits.Int32Bytes;
  public int this[int index] => _ids[index];
  private int[] _ids = new int[8];
  public int Count { get; private set; }

  // False once the selection cap is reached: an order the transport cannot carry is worse than an
  // order over the first MaxSelectedUnits units, so the caller clamps instead of overflowing.
  public bool Add(int unitId) {
    if (Count >= CommandLimits.MaxSelectedUnits)
      return false;

    if (Count == _ids.Length) {
      var capacity = _ids.Length * 2;
      if (capacity > CommandLimits.MaxSelectedUnits)
        capacity = CommandLimits.MaxSelectedUnits;

      var grown = new int[capacity];
      _ids.CopyTo(grown, 0);
      _ids = grown;
    }

    _ids[Count++] = unitId;
    return true;
  }

  public void Clear() {
    Count = 0;
    IsValid = true;
  }

  public void Serialize(ref SpanWriter writer) {
    writer.WriteInt16((short)Count);
    for (var i = 0; i < Count; i++)
      writer.WriteInt32(_ids[i]);
  }

  // Never sizes an array or advances the reader from an unchecked wire count. Commands are pooled, so
  // both Count and IsValid are reassigned on every pass — no state survives from the previous tenant.
  public void Deserialize(ref SpanReader reader) {
    Count = 0;
    IsValid = false;

    int declared = reader.ReadInt16();
    if (declared < 0)
      return;

    var declaredBytes = declared * CommandLimits.Int32Bytes;
    if (declaredBytes > reader.Remaining)
      return;

    if (declared > CommandLimits.MaxSelectedUnits) {
      // The bytes are present, so consume them: catchup and spectator batches deserialize several
      // commands from one reader, and leaving the payload unread would misalign the rest of the batch.
      reader.Skip(declaredBytes);
      return;
    }

    if (_ids.Length < declared)
      _ids = new int[declared];
    for (var i = 0; i < declared; i++)
      _ids[i] = reader.ReadInt32();

    Count = declared;
    IsValid = true;
  }
}
