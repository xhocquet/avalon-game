namespace Meesles.Avalon.Client.Scripts.View;

// Static so the pick survives the lobby -> game scene swap, same as FactionSelection.
public static class GameTypeSelection {
  public static string SelectedGameTypeId = GameTypeCatalog.DefaultId;
}
