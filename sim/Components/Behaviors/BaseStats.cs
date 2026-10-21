using System.Runtime.InteropServices;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// Permanent values while temporary stat modifiers are active.
[KlothoComponent(ComponentIds.BaseStats)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public unsafe partial struct BaseStats : IComponent {
  private fixed long _values[StatRanges.Count];

  public readonly FP64 Get(StatType stat) => FP64.FromRaw(_values[(int)stat]);

  public void Set(StatType stat, FP64 value) =>
    _values[(int)stat] = StatRanges.Clamp(stat, value).RawValue;

  public static BaseStats From(in Stats stats) {
    var baseline = new BaseStats();
    for (var i = 0; i < StatRanges.Count; i++)
      baseline.Set((StatType)i, stats.Get((StatType)i));
    return baseline;
  }
}
