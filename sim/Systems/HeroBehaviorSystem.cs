using System.Collections.Generic;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using Meesles.Avalon.Sim.Heroes;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon;

// Runs each hero's behavior. Snapshot first: a behavior may create or destroy entities, which is
// not safe while its own filter is iterating.
public class HeroBehaviorSystem : ISystem {
  private readonly List<(EntityRef Entity, int HeroAssetId)> _heroes = [];

  public void Update(ref Frame frame) {
    _heroes.Clear();

    var filter = frame.Filter<Hero>();
    while (filter.Next(out var entity)) {
      ref readonly var hero = ref frame.GetReadOnly<Hero>(entity);
      _heroes.Add((entity, hero.HeroAssetId));
    }

    // Both lookups throw on a miss. A hero exists only because HeroFactory resolved the same row,
    // so a miss here is a broken build rather than a state a match should keep running in.
    foreach (var (entity, heroAssetId) in _heroes) {
      var heroAsset = frame.AssetRegistry.Get<HeroAsset>(heroAssetId);
      HeroBehaviors.Get(heroAsset.BehaviorId).OnTick(ref frame, entity, heroAsset);
    }
  }
}
