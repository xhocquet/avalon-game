using Godot;
using Meesles.Avalon.Sim.Models;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon;

// Renders the local hero's owned items into the ItemPanel grid. Purely presentational, the mirror of
// ActionBarController for the buy grid: it polls the hero's Inventory ledger (asset ids appended by
// the sim's shop purchase handler) every HUD sync and paints one cell per distinct owned item, using
// the same icons the shop buy buttons use. Repeatable buys stack, shown as an "xN" badge.
//
// The grid is rebuilt only when the owned-item counts actually change, so steady-state syncs are
// allocation-free (just an int[] compare) and survive rollback: a corrected count simply repaints.
public sealed class InventoryPanelController {
  private const float CellSize = 58f;

  private readonly ShopItemCatalog _catalog;
  private readonly int[] _counts = new int[ShopItemCatalog.ItemDefs.Length];
  private readonly GridContainer _grid;
  private readonly int[] _rendered = new int[ShopItemCatalog.ItemDefs.Length];

  public InventoryPanelController(GridContainer grid, ShopItemCatalog catalog) {
    _grid = grid;
    _catalog = catalog;
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
      for (var i = 0; i < ShopItemCatalog.ItemDefs.Length; i++)
        _counts[i] = inventory.CountOf(ShopItemCatalog.ItemDefs[i].Id);
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
  // owns that item, otherwise a transparent spacer. Keeping the cell count fixed means the panel's
  // footprint never changes as items come and go. Icons/names come from the client catalog; the
  // ledger only carries asset ids.
  private void Rebuild() {
    ClearGrid();

    for (var i = 0; i < _counts.Length; i++) {
      var count = _counts[i];
      if (count <= 0) {
        _grid.AddChild(CreateEmptySlot());
        continue;
      }

      var def = ShopItemCatalog.ItemDefs[i];
      var data = _catalog.Resolve(def.Id);

      var icon = new TextureRect {
        Name = $"Item_{def.Id}",
        CustomMinimumSize = new Vector2(CellSize, CellSize),
        Texture = data.IconTexture,
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        TooltipText = count > 1 ? $"{data.DisplayName} x{count}" : data.DisplayName
      };

      if (count > 1)
        AddCountBadge(icon, count);

      _grid.AddChild(icon);
    }
  }

  // "xN" pinned to the cell's bottom-right, outlined so it reads over any icon (mirrors the shop
  // grid's cost badge).
  private static void AddCountBadge(TextureRect icon, int count) {
    var label = new Label {
      Name = "Count",
      Text = $"x{count}",
      MouseFilter = Control.MouseFilterEnum.Ignore,
      HorizontalAlignment = HorizontalAlignment.Right,
      VerticalAlignment = VerticalAlignment.Bottom
    };
    label.SetAnchorsPreset(Control.LayoutPreset.FullRect);
    label.AddThemeFontSizeOverride("font_size", 13);
    label.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f));
    label.AddThemeConstantOverride("outline_size", 3);
    icon.AddChild(label);
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

  // Fill the grid with transparent, non-interactive spacers so it reserves its full footprint before
  // any item is owned. Visible (not hidden) - a hidden Control is skipped by container layout.
  private void FillEmptySlots() {
    if (_grid == null)
      return;

    for (var i = 0; i < _counts.Length; i++)
      _grid.AddChild(CreateEmptySlot());
  }

  private static Control CreateEmptySlot() {
    return new Control {
      Name = "Empty",
      CustomMinimumSize = new Vector2(CellSize, CellSize),
      MouseFilter = Control.MouseFilterEnum.Ignore
    };
  }
}
