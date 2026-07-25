using System;
using System.Collections.Generic;
using Godot;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Models;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon;

// Drives the contextual ActionGrid in GameUI. It's meant to host a flexible assortment of actions
// depending on what the player has selected; the first (and only, for now) context is the shop.
//
// When the player has a shop selected AND their hero is standing within ShopRules.InteractRange of
// it, the grid fills with one buy button per shop item; otherwise it empties. The grid is rebuilt
// only on the hidden<->shown transition — while shown it just refreshes each item's affordability
// (enabled/greyed) every frame. Proximity here is a UX hint: the sim re-checks gold and range
// authoritatively when the PurchaseItemCommand lands, so a stale/optimistic button is harmless.
public sealed class ActionBarController {
  private const float CellSize = 58f;

  // The grid always holds exactly this many cells so its footprint never changes: buy buttons when a
  // shop is in context, otherwise transparent spacers. Sized to the shop catalog, so the shown and
  // hidden states occupy identical space.
  private static readonly int SlotCount = ShopItemCatalog.ItemDefs.Length;

  private readonly List<ItemButton> _buttons = new();
  private readonly ShopItemCatalog _catalog;
  private readonly GridContainer _grid;
  private readonly Action<int> _onPurchase;
  private bool _shown;

  public ActionBarController(GridContainer grid, ShopItemCatalog catalog, Action<int> onPurchase) {
    _grid = grid;
    _catalog = catalog;
    _onPurchase = onPurchase;
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

    if (!TryGetLocalHero(frame, playerId, out var teamId, out var gold, out var heroX, out var heroZ)) {
      Hide();
      return;
    }

    // Only your own team's shop, and only while the hero is close enough to it.
    if (contextShop.Team != teamId || !WithinRange(heroX, heroZ, contextShop.GlobalPosition)) {
      Hide();
      return;
    }

    Show(frame, gold);
  }

  private static bool WithinRange(float heroX, float heroZ, Vector3 shopPos) {
    var dx = heroX - shopPos.X;
    var dz = heroZ - shopPos.Z;
    var range = (float)ShopRules.InteractRange;
    return dx * dx + dz * dz <= range * range;
  }

  private void Show(Frame frame, int gold) {
    if (!_shown) {
      Build(frame);
      _shown = true;
    }

    foreach (var item in _buttons) {
      var affordable = item.Cost >= 0 && gold >= item.Cost;
      item.Button.Disabled = !affordable;
      item.Button.Modulate = affordable ? Colors.White : new Color(1f, 1f, 1f, 0.4f);
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
      _buttons.Add(new ItemButton(itemId, cost, button));
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
    return frame.AssetRegistry.TryGet<ShopItemAsset>(itemId, out var asset) && asset != null
      ? asset.Cost
      : -1;
  }

  private static bool TryGetLocalHero(Frame frame, int playerId, out int teamId, out int gold,
    out float heroX, out float heroZ) {
    teamId = 0;
    gold = 0;
    heroX = 0f;
    heroZ = 0f;

    var filter = frame.Filter<Hero, Team, Inventory, TransformComponent>();
    while (filter.Next(out var entity)) {
      ref readonly var hero = ref frame.GetReadOnly<Hero>(entity);
      if (hero.PlayerId != playerId)
        continue;

      teamId = frame.GetReadOnly<Team>(entity).TeamId;
      gold = frame.GetReadOnly<Inventory>(entity).Gold;
      ref readonly var transform = ref frame.GetReadOnly<TransformComponent>(entity);
      heroX = transform.Position.x.ToFloat();
      heroZ = transform.Position.z.ToFloat();
      return true;
    }

    return false;
  }

  // Remove every current cell immediately (RemoveChild, not just QueueFree, so the grid never holds
  // the old and new cells together for a frame). Callers repopulate right after.
  private void ClearGrid() {
    if (_grid == null) return;
    foreach (var child in _grid.GetChildren()) {
      _grid.RemoveChild(child);
      child.QueueFree();
    }
  }

  // Fill the grid with transparent, non-interactive spacers so it reserves its full footprint while
  // no shop is in context. Visible (not hidden) - a hidden Control is skipped by container layout,
  // which is exactly the collapse we're preventing.
  private void FillEmptySlots() {
    if (_grid == null) return;
    for (var i = 0; i < SlotCount; i++)
      _grid.AddChild(CreateEmptySlot());
  }

  private static Control CreateEmptySlot() {
    return new Control {
      Name = "Empty",
      CustomMinimumSize = new Vector2(CellSize, CellSize),
      MouseFilter = Control.MouseFilterEnum.Ignore
    };
  }

  private readonly struct ItemButton(int itemId, int cost, TextureButton button) {
    public readonly int ItemId = itemId;
    public readonly int Cost = cost;
    public readonly TextureButton Button = button;
  }
}
