using System.Runtime.InteropServices;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

[KlothoComponent(ComponentIds.Buffs)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public unsafe partial struct Buffs : IComponent {
  public const int MaxEntries = 6;

  public fixed int SourceSkillAssetIds[MaxEntries]; // Originating SkillAsset; 0 marks a free slot
  public fixed int StartedTicks[MaxEntries];
  public fixed int ExpiryTicks[MaxEntries];

  public readonly bool IsActive(int index) => SourceSkillAssetIds[index] != 0;
  public readonly int GetSourceSkillAssetId(int index) => SourceSkillAssetIds[index];
  public readonly int GetStartedTick(int index) => StartedTicks[index];
  public readonly int GetExpiryTick(int index) => ExpiryTicks[index];
  public readonly bool IsExpired(int index, int tick) => IsActive(index) && tick >= ExpiryTicks[index];

  public readonly int GetDurationTicks(int index) =>
    IsActive(index) ? System.Math.Max(0, ExpiryTicks[index] - StartedTicks[index]) : 0;

  public readonly int GetRemainingTicks(int index, int tick) =>
    System.Math.Clamp(ExpiryTicks[index] - tick, 0, GetDurationTicks(index));

  public readonly FP64 GetRemainingFraction(int index, int tick) {
    var duration = GetDurationTicks(index);
    return duration > 0 ? FP64.FromInt(GetRemainingTicks(index, tick)) / FP64.FromInt(duration) : FP64.Zero;
  }

  public readonly int FindSlot(int sourceSkillAssetId) {
    for (var i = 0; i < MaxEntries; i++)
      if (SourceSkillAssetIds[i] == sourceSkillAssetId)
        return i;
    return -1;
  }

  public readonly int FindFreeSlot() {
    for (var i = 0; i < MaxEntries; i++)
      if (!IsActive(i))
        return i;
    return -1;
  }

  public void Set(int index, int sourceSkillAssetId, int startedTick, int expiryTick) {
    SourceSkillAssetIds[index] = sourceSkillAssetId;
    StartedTicks[index] = startedTick;
    ExpiryTicks[index] = expiryTick;
  }

  public void Clear(int index) {
    SourceSkillAssetIds[index] = 0;
    StartedTicks[index] = 0;
    ExpiryTicks[index] = 0;
  }
}
