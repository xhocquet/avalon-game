// The local player's chosen faction, set in the lobby (matchmaking UI) and read by SimCallbacks
// when it sends the one-shot SelectFactionCommand at match start. A process-wide holder so the
// choice survives the lobby -> game scene change without threading it through the handoff struct.
namespace Meesles.Avalon {
  public static class FactionSelection {
    public static int SelectedFactionId = FactionCatalog.DefaultFactionId;
  }
}
