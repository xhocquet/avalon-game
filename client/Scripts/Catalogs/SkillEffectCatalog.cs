using System.Collections.Generic;
using Meesles.Avalon.Sim.Assets;

namespace Meesles.Avalon.Client.Scripts.View;

public class SkillEffectCatalog {
  public const string CrystalBulletScenePath = "res://Scenes/FX/Skills/Crystal.tscn";
  public const string CrystalGiantHardenScenePath = "res://Scenes/FX/Heroes/CrystalGiant/CrystalGiantHarden.tscn";
  public const string CrystalGiantSpikyPunchScenePath = "res://Scenes/FX/Heroes/CrystalGiant/CrystalGiantSpikyPunch.tscn";
  public const string ChargeCircleScenePath = "res://Scenes/FX/Skills/ChargeCircle.tscn";
  public const string ChrysalisBurstScenePath = "res://Scenes/FX/Skills/ChrysalisBurst.tscn";
  public const string HairballScenePath = "res://Scenes/FX/Heroes/HairyWizard/Hairball.tscn";
  public const string AllHairWizardStrangleScenePath = "res://Scenes/FX/Heroes/AllHairWizard/AllHairWizardStrangle.tscn";
  public const string AllHairWizardCloseShaveScenePath = "res://Scenes/FX/Heroes/AllHairWizard/AllHairWizardCloseShave.tscn";
  public const string AllHairWizardBadHairDayScenePath = "res://Scenes/FX/Heroes/AllHairWizard/HairLockExplosion.tscn";
  public const string PickleKnightSlipNSlideScenePath = "res://Scenes/FX/Heroes/PickleKnight/PickleKnightSlipNSlide.tscn";
  public const string PickleKnightRefreshScenePath = "res://Scenes/FX/Heroes/PickleKnight/PickleKnightRefresh.tscn";
  public const string PickleKnightDoubleDipScenePath = "res://Scenes/FX/Heroes/PickleKnight/PickleKnightDoubleDip.tscn";
  public const string PickleKnightExploosionScenePath = "res://Scenes/FX/Heroes/PickleKnight/PickleKnightExploosion.tscn";
  public const string SkinwalkerEatToSurviveScenePath = "res://Scenes/FX/Heroes/Skinwalker/SkinwalkerEatToSurvive.tscn";
  public const string SkinwalkerSprintScenePath = "res://Scenes/FX/Heroes/Skinwalker/SkinwalkerSprint.tscn";
  public const string SkinwalkerDailyPracticeScenePath = "res://Scenes/FX/Heroes/Skinwalker/SkinwalkerDailyPractice.tscn";
  public const string SnailTrailScenePath = "res://Scenes/FX/Heroes/Snailhead/SnailTrail.tscn";
  public const string SnailheadSwivelEyesScenePath = "res://Scenes/FX/Heroes/Snailhead/SnailheadSwivelEyes.tscn";
  public const string SnailheadMoltBuffScenePath = "res://Scenes/FX/Heroes/Snailhead/SnailheadMoltBuff.tscn";
  public const string VenomousSlobberScenePath = "res://Scenes/FX/Heroes/Snailhead/VenomousSlobber.tscn";

  private static readonly HashSet<int> ProjectileEffects = [
    AssetIds.SkillCrystalGiantTertiary,
    AssetIds.SkillHairyWizardPrimary
  ];

  public bool HasProjectileEffect(int skillAssetId) => ProjectileEffects.Contains(skillAssetId);
  public bool HasTetherEffect(int skillAssetId) => skillAssetId == AssetIds.SkillHairyWizardSecondary;
  public bool HasTrailEffect(int skillAssetId) => skillAssetId == AssetIds.SkillSnailheadSecondary;
  public bool HasCastEffect(int skillAssetId) => skillAssetId is
    AssetIds.SkillSnailheadPrimary or AssetIds.SkillHairyWizardUltimate;
}
