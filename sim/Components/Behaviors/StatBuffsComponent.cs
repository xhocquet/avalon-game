using System.Runtime.InteropServices;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// Timed stat modifiers a unit is carrying. An entry stores the exact amount it moved one stat by and
// the tick it comes off, so the revert never has to recompute the modifier - a percentage buff taken
// off after a level-up or an item purchase would otherwise refund an amount it never granted.
//
// Fixed parallel buffers rather than a list, the same reason SkillsComponent uses them: components are
// blittable structs memcpy'd for rollback and the generated codec walks the buffers for hashing. A slot
// is free when its SourceIds entry is 0, so nothing compacts and iteration stays in slot order.
// Size: MaxEntries * (8 + 3 * 4) = 120B, inside the 128-byte component ceiling
[KlothoComponent(ComponentIds.StatBuffs)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public unsafe partial struct StatBuffsComponent : IComponent {
  public const int MaxEntries = 6;

  public fixed long AppliedRaw[MaxEntries]; // Raw 32.32 FP64: what Add actually moved the stat by
  public fixed int SourceIds[MaxEntries]; // SkillAsset id that applied it; 0 marks a free slot
  public fixed int StatTypes[MaxEntries];
  public fixed int ExpiryTicks[MaxEntries];

  public readonly bool IsActive(int index) {
    return SourceIds[index] != 0;
  }

  public readonly int GetSourceId(int index) {
    return SourceIds[index];
  }

  public readonly StatType GetStat(int index) {
    return (StatType)StatTypes[index];
  }

  public readonly FP64 GetApplied(int index) {
    return FP64.FromRaw(AppliedRaw[index]);
  }

  public readonly int GetExpiryTick(int index) {
    return ExpiryTicks[index];
  }

  public readonly bool IsExpired(int index, int tick) {
    return IsActive(index) && tick >= ExpiryTicks[index];
  }

  // A source holds at most one entry per stat, so a recast refreshes its own buff instead of stacking
  // copies of itself. Two different sources on the same stat still get a slot each.
  public readonly int FindSlot(int sourceId, StatType stat) {
    for (var i = 0; i < MaxEntries; i++)
      if (SourceIds[i] == sourceId && StatTypes[i] == (int)stat)
        return i;

    return -1;
  }

  public readonly int FindFreeSlot() {
    for (var i = 0; i < MaxEntries; i++)
      if (SourceIds[i] == 0)
        return i;

    return -1;
  }

  public void Set(int index, int sourceId, StatType stat, FP64 applied, int expiryTick) {
    SourceIds[index] = sourceId;
    StatTypes[index] = (int)stat;
    AppliedRaw[index] = applied.RawValue;
    ExpiryTicks[index] = expiryTick;
  }

  public void Clear(int index) {
    SourceIds[index] = 0;
    StatTypes[index] = 0;
    AppliedRaw[index] = 0;
    ExpiryTicks[index] = 0;
  }
}
