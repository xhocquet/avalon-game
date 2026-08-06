using System.Collections.Generic;

namespace Meesles.Avalon.Sim.Heroes;

public static class HeroSkillSets {
  private static readonly Dictionary<int, IHeroSkillSet> SkillSets = new() {
    [HeroSkillSetIds.HairyWizard] = new HairyWizardSkills(),
    [HeroSkillSetIds.Shroom] = new ShroomSkills(),
    [HeroSkillSetIds.CrystalGiant] = new CrystalGiantSkills(),
    [HeroSkillSetIds.Skinwalker] = new SkinwalkerSkills(),
    [HeroSkillSetIds.PickleKnight] = new PickleKnightSkills()
  };

  public static IHeroSkillSet Get(int skillSetId) {
    if (SkillSets.TryGetValue(skillSetId, out var skillSet))
      return skillSet;

    throw new KeyNotFoundException(
      $"HeroAsset names SkillSetId {skillSetId}, which has no entry in HeroSkillSets.");
  }
}
