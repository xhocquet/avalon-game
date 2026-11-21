using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Factories;

public static class TrainingDummyFactory {
  public const int PlayerId = -1;
  public const int AllyPlayerId = -2;

  public static EntityRef Spawn(ref Frame frame, FPVector3 position, int teamId, int factionId,
    int playerId = PlayerId) {
    var asset = frame.AssetRegistry.Get<HeroStatsAsset>(AssetIds.HeroTrainingDummy);
    var entity = frame.CreateEntity();
    frame.Add(entity, TransformFactory.At(position));
    frame.Add(entity, new Team(teamId));
    frame.Add(entity, new Faction(factionId));
    frame.Add(entity, new Hero(playerId, asset.AssetId));
    frame.Add(entity, new UnitIdentity {
      UnitId = UnitLookup.NextUnitId(ref frame),
      UnitTypeId = SimulationSetup.PlayerUnitTypeId
    });
    frame.Add(entity, Stats.From(asset));
    frame.Add(entity, new Health(asset.BaseHealth) { Mana = asset.BaseMana });
    return entity;
  }
}
