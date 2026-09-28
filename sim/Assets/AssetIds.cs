namespace Meesles.Avalon.Sim.Assets;

// Asset ids are serialized into Assets.bytes and the wire format. Never renumber or reuse one.
// TypeIds identify asset classes; the other ids identify rows. They are separate number spaces.
public static class AssetIds {
  // Single-row assets.
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

  // Factions.
  public const int FactionHairyWizards = 200;
  public const int FactionSnailheads = 201;
  public const int FactionCrystalWarriors = 202;
  public const int FactionSkinwalkerTribe = 203;
  public const int FactionPickleKnights = 204;
  // Next free faction id: 205

  // Shop items.
  public const int ShopItemEyeKey = 300;
  public const int ShopItemFlowerBlade = 301;
  public const int ShopItemPatchCoat = 302;
  public const int ShopItemSmileyBomb = 303;
  public const int ShopItemSpikeBook = 304;
  public const int ShopItemSquirtGun = 305;
  // Next free shop item id: 306

  // Heroes.
  public const int HeroHairyWizard = 400;
  public const int HeroSnailhead = 401;
  public const int HeroCrystalGiant = 402;
  public const int HeroSkinwalker = 403;
  public const int HeroPickleKnight = 404;
  // Next free hero id: 405

  // Four skills per hero, in slot order. Keep this hero-major order aligned with the hero block.
  public const int SkillHairyWizardPrimary = 500;
  public const int SkillHairyWizardSecondary = 501;
  public const int SkillHairyWizardTertiary = 502;
  public const int SkillHairyWizardUltimate = 503;
  public const int SkillSnailheadPrimary = 504;
  public const int SkillSnailheadSecondary = 505;
  public const int SkillSnailheadTertiary = 506;
  public const int SkillSnailheadUltimate = 507;
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

  // Pickup types map to Resources slots by offset. Deleted ids remain holes.
  public const int PickupTypeBase = 600;
  public const int PickupTypeWater = 600;
  // Next free pickup type id: 601

  // Klotho wire type discriminators.
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
