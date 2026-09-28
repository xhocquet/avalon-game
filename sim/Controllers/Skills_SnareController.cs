using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

public static class SnareController {
  // Keeps the later expiry when snares overlap
  public static bool Apply(ref Frame frame, EntityRef entity, int sourceId, int durationTicks) {
    if (sourceId == 0 || durationTicks <= 0)
      return false;

    if (!frame.Has<Snare>(entity))
      frame.Add(entity, new Snare());

    ref var snare = ref frame.Get<Snare>(entity);
    var expiryTick = frame.Tick + durationTicks;
    if (snare.IsSnared && snare.ExpiryTick >= expiryTick)
      return false;

    snare.SourceId = sourceId;
    snare.ExpiryTick = expiryTick;
    return true;
  }

  public static void Clear(ref Frame frame, EntityRef entity) {
    if (frame.Has<Snare>(entity))
      frame.Get<Snare>(entity).Clear();
  }

  public static bool IsSnared(ref Frame frame, EntityRef entity) {
    return frame.Has<Snare>(entity) &&
           frame.GetReadOnly<Snare>(entity).IsSnared;
  }
}
