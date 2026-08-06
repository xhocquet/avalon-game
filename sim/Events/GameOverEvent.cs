using xpTURN.Klotho.Core;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim;

[KlothoSerializable(101)]
// Klotho requires IMatchEndEvent to drive Engine.OnMatchEnded and server room drain.
public partial class GameOverEvent : SimulationEvent, IMatchEndEvent {
  public override EventMode Mode => EventMode.Synced;
  int IMatchEndEvent.WinnerPlayerId => -1;
  FixedString32 IMatchEndEvent.Reason => default;
}
