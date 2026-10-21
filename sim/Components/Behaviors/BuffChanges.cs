using System.Runtime.InteropServices;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// Shared change pool; buff indices refer to slots in the unit's Buffs component.
[KlothoComponent(ComponentIds.BuffChanges)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public unsafe partial struct BuffChanges : IComponent {
  public const int MaxEntries = 6;

  public fixed int BuffSlots[MaxEntries]; // Buff index + 1; 0 marks a free slot
  public fixed int Kinds[MaxEntries];
  public fixed int Parameters[MaxEntries];
  public fixed long MagnitudesRaw[MaxEntries]; // Authored flat amount or percentage

  public readonly bool IsActive(int index) => BuffSlots[index] != 0;
  public readonly int GetBuffIndex(int index) => BuffSlots[index] - 1;
  public readonly BuffChange GetChange(int index) => new(
    (BuffChangeKind)Kinds[index], Parameters[index],
    FP64.FromRaw(MagnitudesRaw[index]));

  public readonly bool HasStatChanges {
    get {
      for (var i = 0; i < MaxEntries; i++)
        if (IsActive(i) && GetChange(i).IsStat)
          return true;
      return false;
    }
  }

  public readonly bool HasChanges(int buffIndex) {
    for (var i = 0; i < MaxEntries; i++)
      if (BuffSlots[i] == buffIndex + 1)
        return true;
    return false;
  }

  public readonly int FindFreeSlot() {
    for (var i = 0; i < MaxEntries; i++)
      if (!IsActive(i))
        return i;
    return -1;
  }

  public void Set(int index, int buffIndex, in BuffChange change) {
    BuffSlots[index] = buffIndex + 1;
    Kinds[index] = (int)change.Kind;
    Parameters[index] = change.Parameter;
    MagnitudesRaw[index] = change.Magnitude.RawValue;
  }

  public void Clear(int index) {
    BuffSlots[index] = 0;
    Kinds[index] = 0;
    Parameters[index] = 0;
    MagnitudesRaw[index] = 0;
  }
}
