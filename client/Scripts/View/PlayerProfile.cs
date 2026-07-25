namespace Meesles.Avalon.Client.Scripts.View;

// Local-only player identity for now. The lobby name field writes here; nothing sends it to the
// server yet (Klotho's IPlayerInfo.DisplayName is still unset), so remote slots fall back to "PN".
public static class PlayerProfile {
  public const string DefaultName = "Player";
  public static string PlayerName = DefaultName;
}
