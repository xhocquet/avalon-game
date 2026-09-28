using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

public static class SilenceController {
  // Keeps the later expiry when silences overlap
  public static bool Apply(ref Frame frame, EntityRef entity, int sourceId, int durationTicks) {
    if (sourceId == 0 || durationTicks <= 0)
      return false;

    if (!frame.Has<Silence>(entity))
      frame.Add(entity, new Silence());

    ref var silence = ref frame.Get<Silence>(entity);
    var expiryTick = frame.Tick + durationTicks;
    if (silence.IsSilenced && silence.ExpiryTick >= expiryTick)
      return false;

    silence.SourceId = sourceId;
    silence.ExpiryTick = expiryTick;
    return true;
  }

  public static void Clear(ref Frame frame, EntityRef entity) {
    if (frame.Has<Silence>(entity))
      frame.Get<Silence>(entity).Clear();
  }

  public static bool IsSilenced(ref Frame frame, EntityRef entity) {
    return frame.Has<Silence>(entity) &&
           frame.GetReadOnly<Silence>(entity).IsSilenced;
  }
}
