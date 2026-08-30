using System;
using System.Collections.Generic;
using Godot;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon;

// Drives the contextual ActionGrid in GameUI. It's meant to host a flexible assortment of actions
// depending on what the player has selected; the first (and only, for now) context is the shop.
//
// When the player has their own team's shop selected AND their hero is in range of it, the grid fills
// with one buy button per shop item; otherwise it empties. The grid is rebuilt only on the
// hidden<->shown transition — while shown it just refreshes each item's buyability (enabled/greyed)
// every frame.
//
// Both the visibility test and the per-button state run through ShopActions — the same rules
// PurchaseItemCommand is judged by — rather than re-deriving them here. Range in particular: the
// ShopEntity node's transform comes from World.tscn and the sim's from the MapLayoutAsset Shop marker,
// so measuring against the node would enable a button the sim then silently rejects wherever the two
// disagree.
public class ActionBarController {
  private const float CellSize = 58f;

  // Our cell count is fixed (this plus _leadingRowPad) so the footprint never changes: buy buttons when
  // a shop is in context, otherwise transparent spacers. Sized to the shop catalog, so the shown and
  // hidden states occupy identical space.
  private static readonly int SlotCount = ShopItemCatalog.ItemDefs.Length;

  private readonly List<ItemButton> _buttons = new();
  private readonly ShopItemCatalog _catalog;
  private readonly GridContainer _grid;
  private readonly Action<int> _onPurchase;
  private readonly PredictedPurchaseState _predicted;

  // Leading cells owned by another controller (SkillBarController's four skill slots). Everything from
  // this index on is ours to clear and rebuild; anything before it we never touch.
  private readonly int _reservedLeadingCells;

  // Spacers laid before our first cell so the buy grid always begins on a fresh row under the skill
  // row, whatever the reserved count and column count work out to. 0 when the skills already fill a row.
  private readonly int _leadingRowPad;
  private bool _shown;

  public ActionBarController(GridContainer grid, ShopItemCatalog catalog, Action<int> onPurchase,
    int reservedLeadingCells = 0, PredictedPurchaseState predicted = null) {
    _grid = grid;
    _catalog = catalog;
    _onPurchase = onPurchase;
    _predicted = predicted;
    _reservedLeadingCells = reservedLeadingCells;
    var columns = _grid?.Columns ?? 0;
    _leadingRowPad = columns > 0 ? (columns - _reservedLeadingCells % columns) % columns : 0;
    ClearGrid();
    FillEmptySlots();
  }

  // Called every HUD sync (GameUI.SyncFromFrame). Decides whether the shop actions should be
  // visible and, if so, keeps their affordability current.
  public void Update(Frame frame, int? localPlayerId, ShopEntity contextShop) {
    if (_grid == null || frame == null || contextShop == null || localPlayerId is not int playerId) {
      Hide();
      return;
    }

    if (!TryGetLocalHero(frame, playerId, out var hero, out var teamId)) {
      Hide();
      return;
    }

    // Only your own team's shop, and only while the hero is close enough to it.
    if (contextShop.Team != teamId || !ShopActions.IsHeroNearTeamShop(ref frame, hero)) {
      Hide();
      return;
    }

    Show(frame, playerId);
  }

  private void Show(Frame frame, int playerId) {
    if (!_shown) {
      Build(frame);
      _shown = true;
    }

    // ShopActions.CanPurchase is the same predicate the sim judges the command by, so a greyed button
    // and a rejected buy can never disagree. Asked against the buys already queued too, so gold the
    // predicted frame has not deducted yet cannot be spent twice.
    var pendingGold = _predicted?.PendingGold ?? 0;
    var pendingItems = _predicted?.PendingItems ?? 0;

    foreach (var item in _buttons) {
      var buyable = ShopActions.CanPurchase(ref frame, playerId, item.ItemId, pendingGold, pendingItems);
      item.Button.Disabled = !buyable;
      item.Button.Modulate = buyable ? Colors.White : new Color(1f, 1f, 1f, 0.4f);
    }
  }

  private void Hide() {
    if (!_shown) return;

    ClearGrid();
    _buttons.Clear();
    FillEmptySlots();
    _shown = false;
  }

  // One buy button per catalog item, in the catalog's declared order so the grid is stable. Cost
  // comes from the ShopItemAsset (sim data); the icon and display name come from the client catalog.
  private void Build(Frame frame) {
    ClearGrid();
    _buttons.Clear();

    for (var i = 0; i < _leadingRowPad; i++)
      _grid.AddChild(CreateEmptySlot());

    foreach (var def in ShopItemCatalog.ItemDefs) {
      var data = _catalog.Resolve(def.Id);
      var cost = GetCost(frame, def.Id);

      var button = new TextureButton {
        Name = $"Buy_{def.Id}",
        CustomMinimumSize = new Vector2(CellSize, CellSize),
        TextureNormal = data.IconTexture,
        IgnoreTextureSize = true,
        StretchMode = TextureButton.StretchModeEnum.KeepAspectCentered,
        TooltipText = cost >= 0 ? $"{data.DisplayName}\nCost: {cost}g" : data.DisplayName
      };

      var itemId = def.Id;
      button.Pressed += () => _onPurchase?.Invoke(itemId);

      AddCostBadge(button, cost);
      _grid.AddChild(button);
      _buttons.Add(new ItemButton(itemId, button));
    }
  }

  // Small cost number pinned to the button's bottom edge, with an outline so it reads over any icon.
  private static void AddCostBadge(TextureButton button, int cost) {
    if (cost < 0) return;

    var label = new Label {
      Name = "Cost",
      Text = cost.ToString(),
      MouseFilter = Control.MouseFilterEnum.Ignore,
      HorizontalAlignment = HorizontalAlignment.Center,
      VerticalAlignment = VerticalAlignment.Bottom
    };
    label.SetAnchorsPreset(Control.LayoutPreset.FullRect);
    label.AddThemeFontSizeOverride("font_size", 13);
    label.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f));
    label.AddThemeConstantOverride("outline_size", 3);
    button.AddChild(label);
  }

  private static int GetCost(Frame frame, int itemId) {
    return frame.AssetRegistry.TryGet<ShopItemAsset>(itemId, out var asset) ? asset.Cost : -1;
  }

  private static bool TryGetLocalHero(Frame frame, int playerId, out EntityRef heroEntity,
    out int teamId) {
    heroEntity = default;
    teamId = 0;

    var filter = frame.Filter<Hero, Team, Inventory, TransformComponent>();
    while (filter.Next(out var entity)) {
      ref readonly var hero = ref frame.GetReadOnly<Hero>(entity);
      if (hero.PlayerId != playerId)
        continue;

      heroEntity = entity;
      teamId = frame.GetReadOnly<Team>(entity).TeamId;
      return true;
    }

    return false;
  }

  // Remove every cell we own immediately (RemoveChild, not just QueueFree, so the grid never holds
  // the old and new cells together for a frame). Callers repopulate right after. Iterated back to front
  // so removals don't shift the indices still to be visited.
  private void ClearGrid() {
    if (_grid == null) return;
    for (var i = _grid.GetChildCount() - 1; i >= _reservedLeadingCells; i--) {
      var child = _grid.GetChild(i);
      _grid.RemoveChild(child);
      child.QueueFree();
    }
  }

  // Fill the grid with transparent, non-interactive spacers so it reserves its full footprint while
  // no shop is in context. Visible (not hidden) - a hidden Control is skipped by container layout,
  // which is exactly the collapse we're preventing.
  private void FillEmptySlots() {
    if (_grid == null) return;
    for (var i = 0; i < _leadingRowPad + SlotCount; i++)
      _grid.AddChild(CreateEmptySlot());
  }

  private static Control CreateEmptySlot() {
    return new Control {
      Name = "Empty",
      CustomMinimumSize = new Vector2(CellSize, CellSize),
      MouseFilter = Control.MouseFilterEnum.Ignore
    };
  }

  private readonly struct ItemButton(int itemId, TextureButton button) {
    public readonly int ItemId = itemId;
    public readonly TextureButton Button = button;
  }
}
