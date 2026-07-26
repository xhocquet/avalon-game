using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// Tunable stat block applied to a hero.
[KlothoComponent(ComponentIds.Stats)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct Stats() : IComponent {
  public int Strength = 100;
  public int Defense = 100;
  public int Speed = 100;

  // Passive gold income per accrual tick, seeded from PlayerStatsAsset at spawn. Defaults to 0 so
  // entities that never had an income (minions, turrets) can't earn one by carrying an Inventory.
  public int GoldPerTick = 0;

  // Damage this entity deals per attack. Clamped at 0 so a debuff can never heal the target.
  public readonly int AttackDamage => Strength < 0 ? 0 : Strength;

  // Single entry point for stat changes (shop items today, leveling/buffs later) so every source
  // routes a StatType through the same switch instead of touching fields directly.
  public void Add(StatType statType, int delta) {
    switch (statType) {
      case StatType.Strength:
        Strength += delta;
        break;
      case StatType.GoldPerTick:
        GoldPerTick += delta;
        break;
    }
  }
}
