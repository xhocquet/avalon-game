using Godot;
using Meesles.Avalon.Client;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon;

// Renders the local hero's owned items into the ItemPanel grid. Purely presentational, the mirror of
// ActionBarController for the buy grid: it polls the hero's Inventory ledger (asset ids appended by
// the sim's shop purchase handler) every HUD sync and paints one cell per distinct owned item, using
// the same icons the shop buy buttons use. Repeatable buys stack, shown as an "xN" badge.
//
// The grid is rebuilt only when the owned-item counts actually change, so steady-state syncs are
// allocation-free (just an int[] compare) and survive rollback: a corrected count simply repaints.
public class InventoryPanelController {
  private static readonly PackedScene ItemIconScene = GD.Load<PackedScene>("res://Scenes/UI/Icons/ItemIcon.tscn");

  private readonly ShopItemCatalog _catalog;
  private readonly int[] _counts = new int[ShopItemCatalog.ItemDefs.Length];
  private readonly GridContainer _grid;
  private readonly int[] _rendered = new int[ShopItemCatalog.ItemDefs.Length];
  private readonly PredictedPurchaseState _predicted;

  public InventoryPanelController(GridContainer grid, ShopItemCatalog catalog,
    PredictedPurchaseState predicted = null) {
    _grid = grid;
    _catalog = catalog;
    _predicted = predicted;
    ClearGrid();
    FillEmptySlots();
  }

  // Called every HUD sync (GameUI.SyncFromFrame). Recounts the local hero's items in catalog order,
  // then repaints only if that differs from what's on screen.
  public void Update(Frame frame, int? localPlayerId) {
    System.Array.Clear(_counts, 0, _counts.Length);
    if (_grid != null && frame != null && localPlayerId is int playerId)
      FillCounts(frame, playerId);

    RebuildIfChanged();
  }

  private void FillCounts(Frame frame, int playerId) {
    var filter = frame.Filter<Hero, Inventory>();
    while (filter.Next(out var entity)) {
      ref readonly var hero = ref frame.GetReadOnly<Hero>(entity);
      if (hero.PlayerId != playerId)
        continue;

      ref readonly var inventory = ref frame.GetReadOnly<Inventory>(entity);

      // Queued buys count as owned, so the icon lands on the same frame as the click and the sim's
      // ledger takes over when the command runs. GameUI ages the optimistic entries before this runs.
      for (var i = 0; i < ShopItemCatalog.ItemDefs.Length; i++) {
        var itemId = ShopItemCatalog.ItemDefs[i].Id;
        _counts[i] = inventory.CountOf(itemId) + (_predicted?.OutstandingFor(itemId) ?? 0);
      }

      return;
    }
  }

  private void RebuildIfChanged() {
    if (CountsUnchanged())
      return;

    System.Array.Copy(_counts, _rendered, _counts.Length);
    Rebuild();
  }

  private bool CountsUnchanged() {
    for (var i = 0; i < _counts.Length; i++)
      if (_counts[i] != _rendered[i])
        return false;

    return true;
  }

  // Always emit one cell per catalog slot, in the catalog's declared order: an icon where the hero
  // owns that item, otherwise a placeholder. Keeping the cell count fixed means the panel's
  // footprint never changes as items come and go. Icons/names come from the client catalog; the
  // ledger only carries asset ids.
  private void Rebuild() {
    ClearGrid();

    for (var i = 0; i < _counts.Length; i++) {
      var count = _counts[i];
      if (count <= 0) {
        _grid.AddChild(CreateEmptySlot(i));
        continue;
      }

      var def = ShopItemCatalog.ItemDefs[i];
      var data = _catalog.Resolve(def.Id);

      var icon = ItemIconScene.Instantiate<TooltipTextureRect>();
      icon.Name = $"Item_{def.Id}";
      icon.Texture = data.IconTexture;
      icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
      icon.TooltipText = count > 1 ? $"{data.DisplayName}\nQuantity: {count}" : data.DisplayName;
      icon.GetNode<Label>("Hotkey").Text = (i + 1).ToString();
      var number = icon.GetNode<Label>("Number");
      number.Text = $"x{count}";
      number.Visible = count > 1;

      _grid.AddChild(icon);
    }
  }

  // Remove every current cell immediately (RemoveChild, not just QueueFree, so the grid never holds
  // old and new cells together for a frame). Callers repopulate right after.
  private void ClearGrid() {
    if (_grid == null)
      return;

    foreach (var child in _grid.GetChildren()) {
      _grid.RemoveChild(child);
      child.QueueFree();
    }
  }

  // Fill the grid with non-interactive placeholders so it reserves its full footprint before any item
  // is owned.
  private void FillEmptySlots() {
    if (_grid == null)
      return;

    for (var i = 0; i < _counts.Length; i++)
      _grid.AddChild(CreateEmptySlot(i));
  }

  private static TextureRect CreateEmptySlot(int slot) {
    var icon = ItemIconScene.Instantiate<TooltipTextureRect>();
    icon.Name = "Empty";
    icon.MouseFilter = Control.MouseFilterEnum.Ignore;
    icon.GetNode<Label>("Hotkey").Text = (slot + 1).ToString();
    return icon;
  }
}
