using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

// The one place a unit is silenced or let go. A silence takes skill casts away and nothing else - the
// unit keeps moving, turning, and auto-attacking - and is enforced in one spot,
// SkillActions.EvaluateCast, rather than by zeroing a stat.
//
// Like a snare, the duration is an absolute expiry tick rather than a countdown, so TimedEffectSystem
// is one comparison per silenced unit and a rollback replay ends the hold on the tick it first did.
public static class Silences {
  // Silences the unit for durationTicks. Overlapping silences keep whichever ends later, so a short
  // one landing on a long one cannot cut it short. Returns false when the silence is a no-op.
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
