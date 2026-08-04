using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using Meesles.Avalon.Sim.Heroes;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Factories;

public static class HeroFactory {
  public static EntityRef Spawn(ref Frame frame, HeroAsset heroAsset, MatchRulesAsset matchRules,
    FPVector3 position, int playerId, int teamId, int factionId) {
    var entity = frame.CreateEntity();

    frame.Add(entity, TransformFactory.At(position));
    frame.Add(entity, new OwnerComponent { OwnerId = playerId });
    frame.Add(entity, new Player { PlayerId = playerId });
    frame.Add(entity, new TeamComponent(teamId));
    frame.Add(entity, new FactionComponent(factionId));
    frame.Add(entity, new Hero(playerId, heroAsset.AssetId));
    frame.Add(entity, new UnitIdComponent {
      UnitId = UnitLookup.NextUnitId(ref frame),
      UnitTypeId = SimulationSetup.PlayerUnitTypeId
    });
    frame.Add(entity, new Controllable());
    frame.Add(entity, new InventoryComponent());
    frame.Add(entity, new ExperienceComponent());
    frame.Add(entity, new StatsComponent {
      Strength = heroAsset.AttackDamage,
      MaxHealth = heroAsset.Health,
      MoveSpeed = heroAsset.MoveSpeed,
      GoldPerTick = matchRules.StartingGoldPerTick
    });
    frame.Add(entity, new Health(heroAsset.Health));
    frame.Add(entity, Combat.From(heroAsset));
    frame.Add(entity, NavAgentFactory.At(ref frame, position, heroAsset.MoveSpeed, heroAsset.Radius));

    // Register hero-specific logic
    HeroBehaviors.Get(heroAsset.BehaviorId).OnSpawn(ref frame, entity, heroAsset);

    return entity;
  }
}
