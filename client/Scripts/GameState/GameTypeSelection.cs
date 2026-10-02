namespace Meesles.Avalon.Client.Scripts.GameState;

// Static so the pick survives the lobby -> game scene swap, same as FactionSelection.
public static class GameTypeSelection {
  public static string SelectedGameTypeId = GameTypeCatalog.DefaultId;
}
