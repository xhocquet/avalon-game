namespace Meesles.Avalon.Client.Scripts.GameState;

// Puts an attack clip's contact frame on the tick the sim deals damage. The clip and the authored
// AttackWindup are tuned independently, so playing the clip at 1x lands the visible hit wherever the
// animator happened to put it - ahead of the damage on a fast windup, behind it on a slow one.
public static class AttackPlaybackSpeed {
  private const float MinScale = 0.25f;
  private const float MaxScale = 4.0f;

  public static float For(float contactTime, float windupSeconds) {
    if (contactTime <= 0.0f || windupSeconds <= 0.0f)
      return 1.0f;

    var scale = contactTime / windupSeconds;
    return scale < MinScale ? MinScale : scale > MaxScale ? MaxScale : scale;
  }
}
