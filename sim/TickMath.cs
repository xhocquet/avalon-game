using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

public static class TickMath {
  private const int FallbackDeltaTimeMs = 16; // frames that have not been given a delta time yet

  public static int DeltaTimeMs(ref Frame frame) {
    return frame.DeltaTimeMs > 0 ? frame.DeltaTimeMs : FallbackDeltaTimeMs;
  }

  // Authored milliseconds -> ticks, rounded up so a duration never expires early.
  public static int MsToTicksCeil(ref Frame frame, int milliseconds) {
    if (milliseconds <= 0)
      return 0;

    var deltaTimeMs = DeltaTimeMs(ref frame);
    return (milliseconds + deltaTimeMs - 1) / deltaTimeMs;
  }
}
