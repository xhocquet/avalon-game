using System.Collections.Generic;
using Meesles.Avalon.Sim.Assets;

namespace Meesles.Avalon.Client.Scripts.View;

public class SkillEffectCatalog {
  public const string CrystalBulletScenePath = "res://Scenes/FX/Skills/Crystal.tscn";
  public const string CrystalGiantHardenScenePath = "res://Scenes/FX/Skills/CrystalGiantHarden.tscn";
  public const string CrystalGiantSpikyPunchScenePath = "res://Scenes/FX/Skills/CrystalGiantSpikyPunch.tscn";
  public const string ChargeCircleScenePath = "res://Scenes/FX/Skills/ChargeCircle.tscn";
  public const string ChrysalisBurstScenePath = "res://Scenes/FX/Skills/ChrysalisBurst.tscn";
  public const string HairballScenePath = "res://Scenes/FX/Skills/Hairball.tscn";
  public const string AllHairWizardStrangleScenePath = "res://Scenes/FX/Skills/AllHairWizardStrangle.tscn";
  public const string AllHairWizardCloseShaveScenePath = "res://Scenes/FX/Skills/AllHairWizardCloseShave.tscn";
  public const string AllHairWizardBadHairDayScenePath = "res://Scenes/FX/Skills/HairLockExplosion.tscn";
  public const string PickleKnightRefreshScenePath = "res://Scenes/FX/Skills/PickleKnightRefresh.tscn";
  public const string PickleKnightDoubleDipScenePath = "res://Scenes/FX/Skills/PickleKnightDoubleDip.tscn";
  public const string PickleKnightExploosionScenePath = "res://Scenes/FX/Skills/PickleKnightExploosion.tscn";
  public const string SkinwalkerEatToSurviveScenePath = "res://Scenes/FX/Skills/SkinwalkerEatToSurvive.tscn";
  public const string SkinwalkerSprintScenePath = "res://Scenes/FX/Skills/SkinwalkerSprint.tscn";
  public const string SkinwalkerDailyPracticeScenePath = "res://Scenes/FX/Skills/SkinwalkerDailyPractice.tscn";
  public const string SnailTrailScenePath = "res://Scenes/FX/Skills/SnailTrail.tscn";
  public const string SnailheadSwivelEyesScenePath = "res://Scenes/FX/Skills/SnailheadSwivelEyes.tscn";
  public const string SnailheadMoltBuffScenePath = "res://Scenes/FX/Skills/SnailheadMoltBuff.tscn";
  public const string VenomousSlobberScenePath = "res://Scenes/FX/Skills/VenomousSlobber.tscn";

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
