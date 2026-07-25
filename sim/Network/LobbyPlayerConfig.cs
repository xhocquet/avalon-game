using xpTURN.Klotho.Core;
using xpTURN.Klotho.Network;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Network;

// Lobby-only broadcast of a player's faction pick, so every client can render the other slots'
// portraits before the match starts. Flow: client -> server -> broadcast to all peers; each peer's
// engine stores it under the sender's playerId (IKlothoEngine.TryGetPlayerConfig).
//
// This is presentation data, NOT a simulation input: the authoritative faction still enters the sim
// through SelectFactionCommand at match start (see SimCallbacks / CommandSystem). PlayerConfig is
// unverified and off the deterministic path by design — keep it that way.
//
// Lives in sim/ because it is the only source root both the Godot client and the dedicated server
// compile, and both ends need the identical type registered with the same wire id.
// 200 == NetworkMessageType.UserDefined_Start, the first id reserved for game-defined messages.
[KlothoSerializable(MessageTypeId = (NetworkMessageType)200)]
public partial class LobbyPlayerConfig : PlayerConfigBase {
  [KlothoOrder] public int FactionId;
}
