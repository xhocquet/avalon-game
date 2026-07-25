// Client-side presentation map for shop items.
// Item IDs match the ShopItemAsset.AssetIds in client/Sim/Data/Assets.json (300 range).
// Sim owns the mechanical data (Cost, AttackBonus); this catalog owns the portraits and
// display names, which are presentation-only and never touch the deterministic sim.

using System.Collections.Generic;
using Godot;

namespace Meesles.Avalon;

public sealed class ShopItemCatalog {
  public const int EyeKeyId = 300;
  public const int FlowerBladeId = 301;
  public const int PatchCoatId = 302;
  public const int SmileyBombId = 303;
  public const int SpikeBookId = 304;
  public const int SquirtGunId = 305;

  public static readonly ShopItemDef[] ItemDefs = [
    new(EyeKeyId, "Eye Key", "res://Assets/Portraits/Items/eye_key.png"),
    new(FlowerBladeId, "Flower Blade", "res://Assets/Portraits/Items/flower_blade.png"),
    new(PatchCoatId, "Patch Coat", "res://Assets/Portraits/Items/patch_coat.png"),
    new(SmileyBombId, "Smiley Bomb", "res://Assets/Portraits/Items/smileybomb.png"),
    new(SpikeBookId, "Spike Book", "res://Assets/Portraits/Items/spike_book.png"),
    new(SquirtGunId, "Squirt Gun", "res://Assets/Portraits/Items/squirt_gun.png")
  ];

  private readonly Dictionary<int, ShopItemData> _byId = new();

  private ShopItemCatalog(IEnumerable<ShopItemData> entries) {
    foreach (var e in entries)
      _byId[e.ItemId] = e;
  }

  public IReadOnlyCollection<ShopItemData> Entries => _byId.Values;

  public ShopItemData Resolve(int itemId) {
    return _byId.TryGetValue(itemId, out var entry)
      ? entry
      : throw new KeyNotFoundException($"No shop item registered for id {itemId}.");
  }

  public static ShopItemCatalog CreateDefault() {
    var entries = new List<ShopItemData>();
    foreach (var def in ItemDefs)
      entries.Add(new ShopItemData {
        ItemId = def.Id,
        DisplayName = def.Name,
        IconTexture = GD.Load<Texture2D>(def.IconTexturePath)
      });

    return new ShopItemCatalog(entries);
  }

  public readonly struct ShopItemDef(
    int id,
    string name,
    string iconTexturePath) {
    public readonly int Id = id;
    public readonly string Name = name;
    public readonly string IconTexturePath = iconTexturePath;
  }

  public sealed class ShopItemData {
    public string DisplayName;
    public int ItemId;
    public Texture2D IconTexture;
  }
}
