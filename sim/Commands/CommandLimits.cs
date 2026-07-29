namespace Meesles.Avalon.Sim.Commands;

// Envelope every command payload is checked against before a handler sees it. These are wire safety
// limits rather than gameplay tuning, so they stay in code instead of Assets.json: they have to be
// identical on client and server (a mismatch makes prediction diverge from the authority) and stable
// across recorded replays.
public static class CommandLimits {
  // Selection cap for unit orders, derived from the transport rather than from taste. Client input
  // rides ClientInputMessage, which is Unreliable, and LiteNetLib refuses to fragment an unreliable
  // packet (TooBigPacketException on send), so a whole order has to fit one datagram at the
  // pre-discovery InitialMtu of 1024 bytes: 1024 - 5 packet header - 13 ClientInputMessage
  // - 28 MoveCommand header - 2 count leaves ~976 bytes, or 244 ids. 192 keeps headroom and sits far
  // below short.MaxValue, so the Int16 count field can never truncate.
  public const int MaxSelectedUnits = 192;

  // Bound on commanded world coordinates. The map spans roughly ±44, so this is ~20x the playable
  // area while staying orders of magnitude below where Q32.32 products saturate and squared-distance
  // sums wrap negative — a wrapped sum makes every `sqrDistance <= range * range` check pass.
  public const int MaxWorldCoordinate = 1024;
}
