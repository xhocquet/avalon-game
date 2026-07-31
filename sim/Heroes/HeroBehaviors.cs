using System.Collections.Generic;

namespace Meesles.Avalon.Sim.Heroes;

public static class HeroBehaviors {
  private static readonly Dictionary<int, IHeroBehavior> Behaviors = new() {
    [HeroBehaviorIds.Default] = new DefaultHeroBehavior()
  };

  public static IHeroBehavior Get(int behaviorId) {
    if (Behaviors.TryGetValue(behaviorId, out var behavior))
      return behavior;

    throw new KeyNotFoundException($"HeroAsset names BehaviorId {behaviorId}, which has no entry in HeroBehaviors.");
  }
}
