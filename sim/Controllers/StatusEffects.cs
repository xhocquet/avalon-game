using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

// The one call behind a cleanse. Every negative status a unit can carry lives in its own component
// with its own helper - a signed StatBuffs entry, a Snare, a DamageOverTime, a Silence - and this
// strips the whole set in one pass. RespawnSystem clears the same four on death; a skill's
// ClearsDebuffs flag reaches for this mid-life.
public static class StatusEffects {
  public static void ClearNegative(ref Frame frame, EntityRef entity) {
    StatBuffApplication.ClearHarmful(ref frame, entity);
    Snares.Clear(ref frame, entity);
    DamageOverTimes.Clear(ref frame, entity);
    Silences.Clear(ref frame, entity);
  }

  public static bool HasAnyNegative(ref Frame frame, EntityRef entity) {
    return StatBuffApplication.HasHarmful(ref frame, entity)
           || Snares.IsSnared(ref frame, entity)
           || DamageOverTimes.IsBurning(ref frame, entity)
           || Silences.IsSilenced(ref frame, entity);
  }
}
