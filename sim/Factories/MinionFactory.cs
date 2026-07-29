using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Factories;

public static class MinionFactory {
  public static EntityRef Spawn(ref Frame frame, MinionStatsAsset stats, FPVector3 position, FP64 facing,
    int teamId, int waveId) {
    var entity = frame.CreateEntity();

    frame.Add(entity, TransformFactory.At(position, facing));
    frame.Add(entity, new Unit {
      UnitId = UnitLookup.NextUnitId(ref frame),
      UnitTypeId = SimulationSetup.MinionUnitTypeId
    });
    frame.Add(entity, new OwnerComponent { OwnerId = teamId });
    frame.Add(entity, new Team(teamId));
    frame.Add(entity, new Minion { WaveId = waveId });
    frame.Add(entity, new Controllable());
    frame.Add(entity, new Health(stats.Health));
    frame.Add(entity, new Stats { Strength = stats.AttackDamage });
    frame.Add(entity, new Combat(stats));
    frame.Add(entity, NavAgentFactory.At(ref frame, position, stats.MoveSpeed, stats.Radius));

    return entity;
  }
}
