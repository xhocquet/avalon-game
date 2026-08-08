using xpTURN.Klotho.Core;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim;

// Klotho requires IMatchEndEvent to drive Engine.OnMatchEnded and server room drain. WinnerPlayerId is
// the interface's single-winner view of WinnerTeamId; the team is the real outcome (see MatchOutcome).
[KlothoSerializable(101)]
public partial class GameOverEvent : SimulationEvent, IMatchEndEvent {
  private static readonly FixedString32 UnknownReason = FixedString32.FromString("unknown");
  private static readonly FixedString32 CrystalReason = FixedString32.FromString("crystal");
  private static readonly FixedString32 TimeoutReason = FixedString32.FromString("timeout");

  [KlothoOrder(0)] public int WinnerPlayerId;
  [KlothoOrder(1)] public int WinnerTeamId;
  [KlothoOrder(2)] public int Reason; // MatchEndReason

  public override EventMode Mode => EventMode.Synced;

  int IMatchEndEvent.WinnerPlayerId => WinnerPlayerId;

  // Klotho reads this as a version-stable telemetry key, so the strings are literals rather than
  // enum names that a rename would silently change.
  FixedString32 IMatchEndEvent.Reason => (MatchEndReason)Reason switch {
    MatchEndReason.Crystal => CrystalReason,
    MatchEndReason.Timeout => TimeoutReason,
    _ => UnknownReason
  };
}
