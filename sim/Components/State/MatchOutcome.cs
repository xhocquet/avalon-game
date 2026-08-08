using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// The game's own record of how the match ended, written once by ScoreSystem. Klotho's
// MatchEndStateComponent stays the engine's gate (it carries only Ended + a single WinnerPlayerId and
// its Reason is deliberately not persisted), so the team-shaped outcome and the reason live here.
// WinnerTeamId is authoritative; a player id is only ever derived from it for the engine's benefit.
[KlothoComponent(ComponentIds.MatchOutcome)]
[KlothoSingletonComponent]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct MatchOutcome : IComponent {
  public const int NoWinnerTeamId = -1;

  public int EndTick;
  public int WinnerTeamId;
  public int Reason; // MatchEndReason

  public readonly bool Ended => Reason != (int)MatchEndReason.Unknown;
  public readonly bool IsDraw => WinnerTeamId == NoWinnerTeamId;
}
