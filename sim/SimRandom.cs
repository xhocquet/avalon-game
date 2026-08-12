using xpTURN.Klotho.Core;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

// Every DeterministicRandom stream in the sim derives from the world seed and one of these keys, so
// two features drawing on the same tick can never draw the same numbers. Allocate a new key here
// rather than reusing one with a different index scheme.
public static class SimRandom {
  public const ulong OasisEjectKey = 1;
  public const ulong CriticalStrikeKey = 2;

  // KlothoEngine injects RandomSeedComponent before world init; headless test harnesses that
  // build an EcsSimulation directly (skipping KlothoEngine) don't, so fall back to a fixed seed.
  public static ulong WorldSeed(ref Frame frame) {
    return frame.TryGetSingleton<RandomSeedComponent>(out var entity)
      ? frame.GetReadOnly<RandomSeedComponent>(entity).Seed
      : 0UL;
  }
}
