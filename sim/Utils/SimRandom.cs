using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

// Random streams derive from the world seed, a feature key, and frame data.
public static class SimRandom {
  public const ulong OasisEjectKey = 1;
  public const ulong CriticalStrikeKey = 2;

  // Direct EcsSimulation test harnesses have no KlothoEngine seed component.
  public static ulong WorldSeed(ref Frame frame) {
    return frame.TryGetSingleton<RandomSeedComponent>(out var entity)
      ? frame.GetReadOnly<RandomSeedComponent>(entity).Seed
      : 0UL;
  }
}
