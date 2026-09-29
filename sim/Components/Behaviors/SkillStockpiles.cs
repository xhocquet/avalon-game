using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

[KlothoComponent(ComponentIds.SkillStockpiles)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public unsafe partial struct SkillStockpiles : IComponent {
  public const int MaxEntries = Skills.MaxSlots;

  public fixed int SourceIds[MaxEntries];
  public fixed int Charges[MaxEntries];
  public fixed int NextRechargeTicks[MaxEntries];

  public readonly int Find(int sourceId) {
    for (var i = 0; i < MaxEntries; i++)
      if (SourceIds[i] == sourceId)
        return i;
    return -1;
  }

  public readonly int FindFree() {
    for (var i = 0; i < MaxEntries; i++)
      if (SourceIds[i] == 0)
        return i;
    return -1;
  }

  public void Clear(int index) {
    SourceIds[index] = 0;
    Charges[index] = 0;
    NextRechargeTicks[index] = 0;
  }
}
