using System;
using System.Collections.Generic;
using Meesles.Avalon.Sim.Assets;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Heroes;

// OnSpawn: Called once, immediately after the entity's components exist
// OnTick: Called every tick for every living entity of this hero
public interface IHeroBehavior {
  void OnSpawn(ref Frame frame, EntityRef entity, HeroAsset hero);
  void OnTick(ref Frame frame, EntityRef entity, HeroAsset hero);
}

// Resolves HeroAsset.BehaviorId to the behavior that runs that hero's spawn and tick logic.
public static class HeroBehaviors {
  // Non-deterministic cache
  private static readonly IHeroBehavior[] Loaded = new IHeroBehavior[Enum.GetValues<HeroBehavior>().Length];

  public static IHeroBehavior Get(int behaviorId) {
    if ((uint)behaviorId >= (uint)Loaded.Length)
      throw new KeyNotFoundException(
        $"HeroAsset names BehaviorId {behaviorId}, which is not a HeroBehavior value.");

    return Loaded[behaviorId] ??= Create((HeroBehavior)behaviorId);
  }

  private static IHeroBehavior Create(HeroBehavior behavior) {
    return behavior switch {
      HeroBehavior.Default => new DefaultHeroBehavior(),
      _ => throw new KeyNotFoundException(
        $"HeroBehavior {behavior} has no implementation in HeroBehaviors.Create.")
    };
  }
}
