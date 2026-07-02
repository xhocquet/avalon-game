using xpTURN.Klotho.Core;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim {
  [KlothoSerializable(101)]
  public partial class GameOverEvent : SimulationEvent, IMatchEndEvent {
    public override EventMode Mode => EventMode.Synced;

    // Klotho requires IMatchEndEvent to drive Engine.OnMatchEnded and server room drain.
    // Avalon match result data is read from MatchEndStateComponent through MatchResultReader.
    int IMatchEndEvent.WinnerPlayerId => -1;
    FixedString32 IMatchEndEvent.Reason => default;
  }
}
