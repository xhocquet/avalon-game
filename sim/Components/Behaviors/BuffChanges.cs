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
  public fixed long AppliedRaw[MaxEntries]; // Exact clamped delta, not the authored magnitude

  public readonly bool IsActive(int index) => BuffSlots[index] != 0;
  public readonly int GetBuffIndex(int index) => BuffSlots[index] - 1;
  public readonly BuffChange GetChange(int index) => new(
    (BuffChangeKind)Kinds[index], Parameters[index],
    FP64.FromRaw(AppliedRaw[index]));

  public readonly int FindSlot(int buffIndex, BuffChangeKind kind, int parameter) {
    for (var i = 0; i < MaxEntries; i++)
      if (BuffSlots[i] == buffIndex + 1 && Kinds[i] == (int)kind && Parameters[i] == parameter)
        return i;
    return -1;
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
    AppliedRaw[index] = change.Applied.RawValue;
  }

  public void Clear(int index) {
    BuffSlots[index] = 0;
    Kinds[index] = 0;
    Parameters[index] = 0;
    AppliedRaw[index] = 0;
  }
}
