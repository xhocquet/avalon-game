namespace Meesles.Avalon.Client.Scripts.GameState;

// Static so the pick survives the lobby -> game scene swap, same as GameTypeSelection
public static class FactionSelection {
  public static int SelectedFactionId = FactionCatalog.DefaultFactionId;
}
