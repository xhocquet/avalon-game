using System.Runtime.InteropServices;
using Meesles.Avalon.Sim.Assets;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// Represents an in-progress attack
[KlothoComponent(ComponentIds.Combat)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct Combat : IComponent {
  public FP64 AttackRange;
  public int AttackCooldownTicks;
  public int CooldownRemainingTicks;
  public EntityRef Target;

  public static Combat From(HeroAsset stats) => new() {
    AttackRange = stats.AttackRange,
    AttackCooldownTicks = stats.AttackCooldownTicks
  };

  public static Combat From(MinionStatsAsset stats) => new() {
    AttackRange = stats.AttackRange,
    AttackCooldownTicks = stats.AttackCooldownTicks
  };

  public static Combat From(TurretStatsAsset stats) => new() {
    AttackRange = stats.AttackRange,
    AttackCooldownTicks = stats.AttackCooldownTicks
  };
}
