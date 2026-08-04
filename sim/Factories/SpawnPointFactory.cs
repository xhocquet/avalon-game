using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Factories;

public static class SpawnPointFactory {
  public static EntityRef Spawn(ref Frame frame, FPVector3 position, int teamId) {
    var entity = frame.CreateEntity();

    frame.Add(entity, TransformFactory.At(position));
    frame.Add(entity, new TeamComponent(teamId));
    frame.Add(entity, new SpawnPoint {
      SpawnPointId = teamId,
      UnitTypeId = SimulationSetup.MinionUnitTypeId
    });

    return entity;
  }
}
