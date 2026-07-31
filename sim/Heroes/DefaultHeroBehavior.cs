using Meesles.Avalon.Sim.Assets;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Heroes;

public sealed class DefaultHeroBehavior : IHeroBehavior {
  public void OnSpawn(ref Frame frame, EntityRef entity, HeroAsset hero) { }

  public void OnTick(ref Frame frame, EntityRef entity, HeroAsset hero) { }
}
