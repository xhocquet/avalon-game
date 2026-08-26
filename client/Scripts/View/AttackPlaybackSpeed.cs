namespace Meesles.Avalon.Client.Scripts.View;

// Puts an attack clip's contact frame on the tick the sim deals damage. The clip and the authored
// AttackWindup are tuned independently, so playing the clip at 1x lands the visible hit wherever the
// animator happened to put it - ahead of the damage on a fast windup, behind it on a slow one.
public static class AttackPlaybackSpeed {
  // Wide enough for any sane clip/windup pair, tight enough that a mis-authored one reads as a
  // too-fast swing rather than a single-frame blur or a unit frozen mid-attack.
  private const float MinScale = 0.25f;
  private const float MaxScale = 4.0f;

  // contactTime of 0 means the rig has not been measured; play at 1x rather than guessing.
  public static float For(float contactTime, float windupSeconds) {
    if (contactTime <= 0.0f || windupSeconds <= 0.0f)
      return 1.0f;

    var scale = contactTime / windupSeconds;
    return scale < MinScale ? MinScale : scale > MaxScale ? MaxScale : scale;
  }
}
