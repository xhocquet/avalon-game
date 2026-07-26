namespace Meesles.Avalon.Sim.Assets;

// Central ledger of Klotho data-asset ids. These ids are baked into Assets.bytes and the wire
// format, so they must stay stable across builds: never renumber a live asset, and never reuse the
// id of a deleted one. Allocate from "next free" at the bottom of each block.
//
// Two id planes, unrelated to each other and to ComponentIds — id 100 here is not id 100 there.
// TypeIds at the bottom are the [KlothoDataAsset(typeId)] wire discriminator, one per class.
// Everything else is a runtime AssetId, one per row in client/Sim/Data/Assets.json. Single-instance
// assets reuse their type id as their instance id. Multi-instance assets get a block of their own —
// factions at 200, shop items at 300 — since their rows keep multiplying and would otherwise chew
// through the type-id range.
//
// Kept in numeric order (not grouped by file) so gaps and duplicate ids are visible at a glance.
public static class AssetIds {
  // Single-instance assets: one row each in Assets.json, resolved via AssetRegistry.Get<T>().
  public const int PlayerStats = 100;
  public const int WaveRules = 101;
  public const int MapLayout = 102;
  public const int MinionStats = 103;
  public const int TurretStats = 106;
  public const int CrystalStats = 107;
  public const int ShopRules = 108;
  public const int MovementRules = 109;
  public const int MatchRules = 110;
  public const int PickupRules = 111;
  public const int NavigationTuning = 112;
  public const int CombatRules = 113;

  // FactionAsset rows. Hero scenes and portraits for these ids live in FactionCatalog.
  public const int FactionHairyWizards = 200;
  public const int FactionShrooms = 201;

  // Next free faction id: 202

  // ShopItemAsset rows. Icons and display names for these ids live in ShopItemCatalog.
  public const int ShopItemEyeKey = 300;
  public const int ShopItemFlowerBlade = 301;
  public const int ShopItemPatchCoat = 302;
  public const int ShopItemSmileyBomb = 303;
  public const int ShopItemSpikeBook = 304;
  public const int ShopItemSquirtGun = 305;

  // Next free shop item id: 306

  // What the deserializer dispatches on to pick a type. Every asset class has one, including the
  // multi-instance ones that own no id above.
  public static class TypeIds {
    public const int PlayerStats = 100;
    public const int WaveRules = 101;
    public const int MapLayout = 102;
    public const int MinionStats = 103;
    public const int Faction = 104;
    public const int ShopItem = 105;
    public const int TurretStats = 106;
    public const int CrystalStats = 107;
    public const int ShopRules = 108;
    public const int MovementRules = 109;
    public const int MatchRules = 110;
    public const int PickupRules = 111;
    public const int NavigationTuning = 112;
    public const int CombatRules = 113;

    // Next free type id: 114
  }
}
