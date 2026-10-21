using System;
using System.Runtime.CompilerServices;
using Meesles.Avalon.Sim.Components;

namespace Meesles.Avalon.Sim;

public struct StatusEffectChanges : IEquatable<StatusEffectChanges> {
  [InlineArray(BuffChanges.MaxEntries)]
  private struct ChangeBuffer {
    private StatusEffectChange _element0;
  }

  private ChangeBuffer _items;
  public int Count { get; private set; }

  public readonly StatusEffectChange this[int index] {
    get {
      if ((uint)index >= (uint)Count)
        throw new ArgumentOutOfRangeException(nameof(index));
      return _items[index];
    }
  }

  internal void Add(StatusEffectChange change) {
    _items[Count] = change;
    Count++;
  }

  public readonly Enumerator GetEnumerator() => new(this);

  public readonly bool Equals(StatusEffectChanges other) {
    if (Count != other.Count)
      return false;
    for (var i = 0; i < Count; i++)
      if (!_items[i].Equals(other._items[i]))
        return false;
    return true;
  }

  public override readonly bool Equals(object obj) => obj is StatusEffectChanges other && Equals(other);

  public override readonly int GetHashCode() {
    var hash = Count;
    for (var i = 0; i < Count; i++)
      hash = unchecked(hash * 397 ^ _items[i].GetHashCode());
    return hash;
  }

  public static bool operator ==(StatusEffectChanges left, StatusEffectChanges right) => left.Equals(right);
  public static bool operator !=(StatusEffectChanges left, StatusEffectChanges right) => !left.Equals(right);

  public struct Enumerator {
    private readonly StatusEffectChanges _changes;
    private int _index;

    internal Enumerator(StatusEffectChanges changes) {
      _changes = changes;
      _index = -1;
    }

    public readonly StatusEffectChange Current => _changes[_index];
    public bool MoveNext() => ++_index < _changes.Count;
  }
}
