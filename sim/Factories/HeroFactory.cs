using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Factories;

public static class HeroFactory {
  // Heroes draw their attack profile from MinionStatsAsset and everything else from PlayerStatsAsset.
  public static EntityRef Spawn(ref Frame frame, PlayerStatsAsset playerStats, MinionStatsAsset combatStats,
    FPVector3 position, int playerId, int teamId, int factionId) {
    var entity = frame.CreateEntity();

    frame.Add(entity, TransformFactory.At(position));
    frame.Add(entity, new OwnerComponent { OwnerId = playerId });
    frame.Add(entity, new Player { PlayerId = playerId });
    frame.Add(entity, new Team(teamId));
    frame.Add(entity, new Faction(factionId));
    frame.Add(entity, new Hero(playerId));
    frame.Add(entity, new Unit {
      UnitId = UnitLookup.NextUnitId(ref frame),
      UnitTypeId = SimulationSetup.PlayerUnitTypeId
    });
    frame.Add(entity, new Controllable());
    frame.Add(entity, new Inventory());
    frame.Add(entity, new Stats {
      Strength = combatStats.AttackDamage,
      GoldPerTick = playerStats.StartingGoldPerTick
    });
    frame.Add(entity, new Health(playerStats.Health));
    frame.Add(entity, new Combat(combatStats));
    frame.Add(entity, NavAgentFactory.At(ref frame, position, playerStats.MoveSpeed, playerStats.Radius));

    return entity;
  }
}
