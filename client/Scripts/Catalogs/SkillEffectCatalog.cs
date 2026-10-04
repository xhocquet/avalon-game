using System.Collections.Generic;
using Meesles.Avalon.Sim.Assets;

namespace Meesles.Avalon.Client.Scripts.View;

public class SkillEffectCatalog {
  public const string CrystalBulletScenePath = "res://Scenes/FX/Skills/crystal.tscn";
  public const string HairballScenePath = "res://Scenes/FX/Skills/hairball.tscn";

  private static readonly HashSet<int> ProjectileEffects = [
    AssetIds.SkillCrystalGiantTertiary,
    AssetIds.SkillHairyWizardPrimary
  ];

  public bool HasProjectileEffect(int skillAssetId) => ProjectileEffects.Contains(skillAssetId);
}
