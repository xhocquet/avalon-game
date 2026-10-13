using System.Collections.Generic;
using Meesles.Avalon.Sim.Assets;

namespace Meesles.Avalon.Client.Scripts.View;

public class SkillEffectCatalog {
  public const string CrystalBulletScenePath = "res://Scenes/FX/Skills/Crystal.tscn";
  public const string HairballScenePath = "res://Scenes/FX/Skills/Hairball.tscn";
  public const string PickleKnightRefreshScenePath = "res://Scenes/FX/Skills/PickleKnightRefresh.tscn";
  public const string PickleKnightExploosionScenePath = "res://Scenes/FX/Skills/PickleKnightExploosion.tscn";
  public const string SkinwalkerEatToSurviveScenePath = "res://Scenes/FX/Skills/SkinwalkerEatToSurvive.tscn";
  public const string SkinwalkerSprintScenePath = "res://Scenes/FX/Skills/SkinwalkerSprint.tscn";
  public const string SnailTrailScenePath = "res://Scenes/FX/Skills/SnailTrail.tscn";

  private static readonly HashSet<int> ProjectileEffects = [
    AssetIds.SkillCrystalGiantTertiary,
    AssetIds.SkillHairyWizardPrimary
  ];

  public bool HasProjectileEffect(int skillAssetId) => ProjectileEffects.Contains(skillAssetId);
  public bool HasTrailEffect(int skillAssetId) => skillAssetId == AssetIds.SkillSnailheadSecondary;
}
