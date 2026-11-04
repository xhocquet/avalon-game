using System;
using System.Collections.Generic;
using Godot;
using Meesles.Avalon.Client;
using Meesles.Avalon.Client.Scripts.Entities;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon;

// Owns the contextual shop popup and refreshes purchase availability from the sim.
public class ActionBarController {
  private const float CellSize = 54f;
  private const int SlotCapacity = 12;
  private static Texture2D EmptySlotTexture => GD.Load<Texture2D>("res://Assets/Placeholders/purple-swirl.webp");

  private readonly List<ItemButton> _buttons = new();
  private readonly ShopItemCatalog _catalog;
  private readonly GridContainer _grid;
  private readonly ActionGridPopup _popup;
  private readonly Action<int> _onPurchase;
  private readonly PredictedPurchaseState _predicted;

  private bool _shown;

  public ActionBarController(ActionGridPopup popup, ShopItemCatalog catalog, Action<int> onPurchase,
    PredictedPurchaseState predicted = null) {
    _popup = popup;
    _grid = popup.Grid;
    _catalog = catalog;
    _onPurchase = onPurchase;
    _predicted = predicted;
    _popup.Hide();
    ClearGrid();
    FillEmptySlots();
  }

  public void ResetContext() {
    Hide();
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
    if (contextShop.Team != teamId || !ShopController.IsHeroNearTeamShop(ref frame, hero)) {
      Hide();
      return;
    }

    Show(frame, playerId);
  }

  private void Show(Frame frame, int playerId) {
    if (!_shown) {
      Build(frame);
      _shown = true;
      _popup.Show();
    }

    // ShopController.CanPurchase is the same predicate the sim judges the command by, so a greyed button
    // and a rejected buy can never disagree. Asked against the buys already queued too, so gold the
    // predicted frame has not deducted yet cannot be spent twice.
    var pendingGold = _predicted?.PendingGold ?? 0;
    var pendingItems = _predicted?.PendingItems ?? 0;

    foreach (var item in _buttons) {
      var buyable = ShopController.CanPurchase(ref frame, playerId, item.ItemId, pendingGold, pendingItems);
      item.Button.Disabled = !buyable;
      item.Button.Modulate = buyable ? Colors.White : new Color(1f, 1f, 1f, 0.4f);
      item.PurchaseHint.Visible = buyable;
    }
  }

  private void Hide() {
    _popup.Hide();
    if (!_shown) return;

    ClearGrid();
    _buttons.Clear();
    FillEmptySlots();
    _shown = false;
  }

  // Catalog order is stable across popup opens.
  private void Build(Frame frame) {
    ClearGrid();
    _buttons.Clear();

    var count = Math.Min(ShopItemCatalog.ItemDefs.Length, SlotCapacity);
    for (var i = 0; i < count; i++) {
      var def = ShopItemCatalog.ItemDefs[i];
      var data = _catalog.Resolve(def.Id);
      var cost = GetCost(frame, def.Id);

      var button = new TooltipTextureButton {
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
      var purchaseHint = ActionGridOutline.Create("PurchaseHint");
      button.AddChild(purchaseHint);
      _grid.AddChild(button);
      _buttons.Add(new ItemButton(itemId, button, purchaseHint));
    }

    for (var i = count; i < SlotCapacity; i++)
      _grid.AddChild(CreateEmptySlot());
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

  // Detach cells immediately before repopulating.
  private void ClearGrid() {
    if (_grid == null) return;
    for (var i = _grid.GetChildCount() - 1; i >= 0; i--) {
      var child = _grid.GetChild(i);
      _grid.RemoveChild(child);
      child.QueueFree();
    }
  }

  // Keep twelve cells when the catalog has fewer items.
  private void FillEmptySlots() {
    if (_grid == null) return;
    for (var i = 0; i < SlotCapacity; i++)
      _grid.AddChild(CreateEmptySlot());
  }

  private static TextureRect CreateEmptySlot() {
    return new TextureRect {
      Name = "Empty",
      CustomMinimumSize = new Vector2(CellSize, CellSize),
      MouseFilter = Control.MouseFilterEnum.Ignore,
      Texture = EmptySlotTexture,
      ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
      StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered
    };
  }

  private readonly struct ItemButton(int itemId, TextureButton button, Panel purchaseHint) {
    public readonly int ItemId = itemId;
    public readonly TextureButton Button = button;
    public readonly Panel PurchaseHint = purchaseHint;
  }
}
