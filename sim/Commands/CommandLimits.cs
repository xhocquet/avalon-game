namespace Meesles.Avalon.Sim.Commands;

public static class CommandLimits {
  // Serialized primitive widths.
  public const int Int16Bytes = 2;
  public const int Int32Bytes = 4;
  public const int Fp64Bytes = 8;

  // CommandBase: type, player id, tick.
  public const int HeaderBytes = Int32Bytes * 3;

  // Fits an unreliable input datagram before MTU discovery (1024 bytes).
  // 192 leaves headroom below the ~244 unit-id maximum.
  public const int MaxSelectedUnits = 192;
  public const int MaxWorldCoordinate = 1024;
}
