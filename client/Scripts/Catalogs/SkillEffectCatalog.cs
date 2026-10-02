using System.Collections.Generic;
using Meesles.Avalon.Sim.Assets;

namespace Meesles.Avalon.Client.Scripts.View;

public class SkillEffectCatalog {
  public const string CrystalBulletScenePath = "res://Scenes/FX/Skills/crystal.tscn";

  private static readonly HashSet<int> ProjectileEffects = [AssetIds.SkillCrystalGiantTertiary];

  public bool HasProjectileEffect(int skillAssetId) => ProjectileEffects.Contains(skillAssetId);
}
