using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Factories;

public static class TurretFactory {
  // turretIndex is 1-based per team; the two are packed into TurretId so it stays unique map-wide.
  public static EntityRef Spawn(ref Frame frame, TurretStatsAsset stats, FPVector3 position, int teamId,
    int turretIndex) {
    var entity = frame.CreateEntity();

    frame.Add(entity, TransformFactory.At(position));
    frame.Add(entity, new Unit {
      UnitId = UnitLookup.NextUnitId(ref frame),
      UnitTypeId = SimulationSetup.TurretUnitTypeId
    });
    frame.Add(entity, new Team(teamId));
    frame.Add(entity, new Turret { TurretId = teamId * 100 + turretIndex });
    frame.Add(entity, new Health(stats.Health));
    frame.Add(entity, new Stats { Strength = stats.AttackDamage });
    frame.Add(entity, new Combat {
      AttackRange = stats.AttackRange,
      AttackCooldownTicks = stats.AttackCooldownTicks,
      CooldownRemainingTicks = 0
    });

    return entity;
  }
}
