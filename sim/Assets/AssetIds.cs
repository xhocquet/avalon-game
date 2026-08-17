namespace Meesles.Avalon.Sim.Assets;

// Central ledger of Klotho data-asset ids. These ids are baked into Assets.bytes and the wire
// format, so they must stay stable across builds: never renumber a live asset, and never reuse the
// id of a deleted one. Allocate from "next free" at the bottom of each block.
//
// Two id planes, unrelated to each other and to ComponentIds — id 100 here is not id 100 there.
// TypeIds at the bottom are the [KlothoDataAsset(typeId)] wire discriminator, one per class.
// Everything else is a runtime AssetId, one per row under client/Sim/Data/Assets/. Single-instance
// assets reuse their type id as their instance id. Multi-instance assets get a block of their own —
// factions at 200, shop items at 300, heroes at 400 — since their rows keep multiplying and would
// otherwise chew through the type-id range.
public static class AssetIds {
  // Single-instance assets: one row each under Assets/, resolved via AssetRegistry.Get<T>().
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
  public const int XpRules = 115;
  public const int GoldRules = 118;

  // FactionAsset, FactionCatalog
  public const int FactionHairyWizards = 200;
  public const int FactionShrooms = 201;
  public const int FactionCrystalWarriors = 202;
  public const int FactionSkinwalkerTribe = 203;
  public const int FactionPickleKnights = 204;
  // Next free faction id: 205

  // ShopItemAsset, ShopItemCatalog
  public const int ShopItemEyeKey = 300;
  public const int ShopItemFlowerBlade = 301;
  public const int ShopItemPatchCoat = 302;
  public const int ShopItemSmileyBomb = 303;
  public const int ShopItemSpikeBook = 304;
  public const int ShopItemSquirtGun = 305;
  // Next free shop item id: 306

  // One HeroAsset per hero -> FactionAsset
  public const int HeroHairyWizard = 400;
  public const int HeroShroom = 401;
  public const int HeroCrystalGiant = 402;
  public const int HeroSkinwalker = 403;
  public const int HeroPickleKnight = 404;
  // Next free hero id: 405

  // SkillAsset, SkillCatalog. Four rows per hero in slot order (Primary, Secondary, Tertiary, Ultimate),
  // blocked hero-major in the same order as the Hero* block above. Every hero owns its own rows even
  // where the numbers currently match, so retuning one hero's skill never touches another's.
  public const int SkillHairyWizardPrimary = 500;
  public const int SkillHairyWizardSecondary = 501;
  public const int SkillHairyWizardTertiary = 502;
  public const int SkillHairyWizardUltimate = 503;
  public const int SkillShroomPrimary = 504;
  public const int SkillShroomSecondary = 505;
  public const int SkillShroomTertiary = 506;
  public const int SkillShroomUltimate = 507;
  public const int SkillCrystalGiantPrimary = 508;
  public const int SkillCrystalGiantSecondary = 509;
  public const int SkillCrystalGiantTertiary = 510;
  public const int SkillCrystalGiantUltimate = 511;
  public const int SkillSkinwalkerPrimary = 512;
  public const int SkillSkinwalkerSecondary = 513;
  public const int SkillSkinwalkerTertiary = 514;
  public const int SkillSkinwalkerUltimate = 515;
  public const int SkillPickleKnightPrimary = 516;
  public const int SkillPickleKnightSecondary = 517;
  public const int SkillPickleKnightTertiary = 518;
  public const int SkillPickleKnightUltimate = 519;
  // Next free skill id: 520

  // PickupTypeAsset, one row per collectable resource kind. Index-significant: a type's wallet slot
  // in ResourcesComponent is its offset from PickupTypeBase (see PickupTypes), so a deleted type
  // leaves its id as a hole and the block never grows past PickupTypes.MaxTypes.
  public const int PickupTypeBase = 600;
  public const int PickupTypeWater = 600;
  // Next free pickup type id: 601

  // What the deserializer dispatches on to pick a type. Every asset class has one, including the
  // multi-instance ones that own no id above.
  public static class TypeIds {
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
    public const int Hero = 114;
    public const int XpRules = 115;
    public const int Skill = 116;

    public const int PickupType = 117;
    public const int GoldRules = 118;
    // Next free type id: 119
  }
}
