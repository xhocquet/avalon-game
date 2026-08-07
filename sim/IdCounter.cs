using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

// One global sequence per counter component, allocated lazily so callers never have to order
// Initialize before the first Next.
public static class IdCounter<T> where T : unmanaged, IComponent, IIdCounter {
  public const int FirstId = 1;

  public static void Initialize(ref Frame frame, int nextId = FirstId) {
    if (frame.TryGetSingleton<T>(out _)) return;

    var entity = frame.CreateEntity();
    frame.Add(entity, new T { NextId = nextId });
  }

  public static int Next(ref Frame frame) {
    Initialize(ref frame);

    ref var state = ref frame.GetSingleton<T>();
    var id = state.NextId;
    state.NextId += 1;
    return id;
  }
}
