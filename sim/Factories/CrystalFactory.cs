using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Factories;

public static class CrystalFactory {
  public static EntityRef Spawn(ref Frame frame, CrystalStatsAsset stats, FPVector3 position, int teamId) {
    var entity = frame.CreateEntity();

    frame.Add(entity, TransformFactory.At(position));
    frame.Add(entity, new Unit {
      UnitId = UnitLookup.NextUnitId(ref frame),
      UnitTypeId = SimulationSetup.CrystalUnitTypeId
    });
    frame.Add(entity, new OwnerComponent { OwnerId = teamId });
    frame.Add(entity, new Team(teamId));
    frame.Add(entity, new Crystal { CrystalId = teamId });
    frame.Add(entity, new Health(stats.Health));
    frame.Add(entity, new Stats { MaxHealth = stats.Health });

    return entity;
  }
}
