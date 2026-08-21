using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

// The one place a unit is rooted or let go. A snare takes movement away and nothing else - the unit
// keeps attacking, casting, and turning - so it is a movement rule rather than a stat: it is checked
// by the movers themselves (NavigationAgentSystem, CommandSystem) instead of zeroing MoveSpeed, which
// would fight with every other modifier on the stat and leak through a revert that clamped.
//
// Like a buff, the duration is an absolute expiry tick rather than a countdown, so TimedEffectSystem
// is one comparison per snared unit and a rollback replay ends the hold on the tick it first did.
public static class Snares {
  // Holds the unit for durationTicks. Overlapping snares keep whichever ends later, so a short one
  // landing on a long one cannot cut it short. Returns false when the snare is a no-op.
  public static bool Apply(ref Frame frame, EntityRef entity, int sourceId, int durationTicks) {
    if (sourceId == 0 || durationTicks <= 0)
      return false;

    if (!frame.Has<SnareComponent>(entity))
      frame.Add(entity, new SnareComponent());

    ref var snare = ref frame.Get<SnareComponent>(entity);
    var expiryTick = frame.Tick + durationTicks;
    if (snare.IsSnared && snare.ExpiryTick >= expiryTick)
      return false;

    snare.SourceId = sourceId;
    snare.ExpiryTick = expiryTick;
    return true;
  }

  public static void Clear(ref Frame frame, EntityRef entity) {
    if (frame.Has<SnareComponent>(entity))
      frame.Get<SnareComponent>(entity).Clear();
  }

  public static bool IsSnared(ref Frame frame, EntityRef entity) {
    return frame.Has<SnareComponent>(entity) &&
           frame.GetReadOnly<SnareComponent>(entity).IsSnared;
  }
}
