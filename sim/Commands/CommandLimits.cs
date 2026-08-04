namespace Meesles.Avalon.Sim.Commands;

public static class CommandLimits {
  // Selection cap for unit orders, derived from the transport rather than from taste. Client input
  // rides ClientInputMessage, which is Unreliable, and LiteNetLib refuses to fragment an unreliable
  // packet (TooBigPacketException on send), so a whole order has to fit one datagram at the
  // pre-discovery InitialMtu of 1024 bytes: 1024 - 5 packet header - 13 ClientInputMessage
  // - 28 MoveCommand header - 2 count leaves ~976 bytes, or 244 ids. 192 keeps headroom and sits far
  // below short.MaxValue, so the Int16 count field can never truncate.
  public const int MaxSelectedUnits = 192;
  public const int MaxWorldCoordinate = 1024;
}
