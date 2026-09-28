using xpTURN.Klotho.Core;
using xpTURN.Klotho.Network;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Events;

[KlothoSerializable(MessageTypeId = (NetworkMessageType)200)]
public partial class LobbyPlayerConfig : PlayerConfigBase {
  [KlothoOrder] public int FactionId;
}
