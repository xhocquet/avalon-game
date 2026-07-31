using Meesles.Avalon.Sim.Assets;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Heroes;

// Per-hero logic
public interface IHeroBehavior {
  // Called once, immediately after the entity's components exist
  void OnSpawn(ref Frame frame, EntityRef entity, HeroAsset hero);

  // Called every tick for every living entity of this hero
  void OnTick(ref Frame frame, EntityRef entity, HeroAsset hero);
}
