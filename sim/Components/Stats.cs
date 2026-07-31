using System.Runtime.InteropServices;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// Represents stats belonging to a unit. Changes apply only to this unit.
// Most values come from the faction hero's asset file
[KlothoComponent(ComponentIds.Stats)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct Stats() : IComponent {
  public int Strength = 100;
  public int Defense = 100;
  public int MaxHealth = 0;
  public FP64 MoveSpeed = FP64.Zero;
  public FP64 AttackSpeed = FP64.One;
  public int GoldPerTick = 0; // Seeded from MatchRulesAsset

  public readonly int AttackDamage => Strength < 0 ? 0 : Strength;

  public void Add(StatType statType, FP64 delta) {
    switch (statType) {
      case StatType.Strength:
        Strength += delta.ToInt();
        break;
      case StatType.MaxHealth:
        MaxHealth += delta.ToInt();
        break;
      case StatType.MoveSpeed:
        MoveSpeed += delta;
        break;
      case StatType.AttackSpeed:
        AttackSpeed += delta;
        break;
      case StatType.GoldPerTick:
        GoldPerTick += delta.ToInt();
        break;
    }
  }
}
