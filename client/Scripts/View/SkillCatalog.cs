// Client-side presentation map for hero skills.
// Skill IDs come from the sim-side ledger in sim/Assets/AssetIds.cs and match the SkillAsset rows in
// client/Sim/Data/Assets.json (500 range), four per hero in SkillSlot order.
// Sim owns the mechanical data (MaxRank, CooldownMs); this catalog owns the display names, which are
// presentation-only and never touch the deterministic sim.

using System.Collections.Generic;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Assets;

namespace Meesles.Avalon;

public class SkillCatalog {
  public static readonly SkillDef[] SkillDefs = [
    new(AssetIds.SkillHairyWizardHardHit, AssetIds.HeroHairyWizard, SkillSlot.HardHit, "Hard Hit"),
    new(AssetIds.SkillHairyWizardBuff, AssetIds.HeroHairyWizard, SkillSlot.Buff, "Buff"),
    new(AssetIds.SkillHairyWizardRangeShot, AssetIds.HeroHairyWizard, SkillSlot.RangeShot, "Range Shot"),
    new(AssetIds.SkillHairyWizardUltimate, AssetIds.HeroHairyWizard, SkillSlot.Ultimate, "Ultimate"),
    new(AssetIds.SkillShroomHardHit, AssetIds.HeroShroom, SkillSlot.HardHit, "Hard Hit"),
    new(AssetIds.SkillShroomBuff, AssetIds.HeroShroom, SkillSlot.Buff, "Buff"),
    new(AssetIds.SkillShroomRangeShot, AssetIds.HeroShroom, SkillSlot.RangeShot, "Range Shot"),
    new(AssetIds.SkillShroomUltimate, AssetIds.HeroShroom, SkillSlot.Ultimate, "Ultimate"),
    new(AssetIds.SkillCrystalGiantHardHit, AssetIds.HeroCrystalGiant, SkillSlot.HardHit, "Hard Hit"),
    new(AssetIds.SkillCrystalGiantBuff, AssetIds.HeroCrystalGiant, SkillSlot.Buff, "Buff"),
    new(AssetIds.SkillCrystalGiantRangeShot, AssetIds.HeroCrystalGiant, SkillSlot.RangeShot, "Range Shot"),
    new(AssetIds.SkillCrystalGiantUltimate, AssetIds.HeroCrystalGiant, SkillSlot.Ultimate, "Ultimate"),
    new(AssetIds.SkillSkinwalkerHardHit, AssetIds.HeroSkinwalker, SkillSlot.HardHit, "Hard Hit"),
    new(AssetIds.SkillSkinwalkerBuff, AssetIds.HeroSkinwalker, SkillSlot.Buff, "Buff"),
    new(AssetIds.SkillSkinwalkerRangeShot, AssetIds.HeroSkinwalker, SkillSlot.RangeShot, "Range Shot"),
    new(AssetIds.SkillSkinwalkerUltimate, AssetIds.HeroSkinwalker, SkillSlot.Ultimate, "Ultimate"),
    new(AssetIds.SkillPickleKnightHardHit, AssetIds.HeroPickleKnight, SkillSlot.HardHit, "Hard Hit"),
    new(AssetIds.SkillPickleKnightBuff, AssetIds.HeroPickleKnight, SkillSlot.Buff, "Buff"),
    new(AssetIds.SkillPickleKnightRangeShot, AssetIds.HeroPickleKnight, SkillSlot.RangeShot, "Range Shot"),
    new(AssetIds.SkillPickleKnightUltimate, AssetIds.HeroPickleKnight, SkillSlot.Ultimate, "Ultimate")
  ];

  private readonly Dictionary<int, SkillDef> _byId = new();

  private SkillCatalog(IEnumerable<SkillDef> entries) {
    foreach (var e in entries)
      _byId[e.SkillId] = e;
  }

  public IReadOnlyCollection<SkillDef> Entries => _byId.Values;

  public SkillDef Resolve(int skillId) {
    return _byId.TryGetValue(skillId, out var entry)
      ? entry
      : throw new KeyNotFoundException($"No skill registered for id {skillId}.");
  }

  public static SkillCatalog CreateDefault() {
    return new SkillCatalog(SkillDefs);
  }

  public readonly struct SkillDef(
    int skillId,
    int heroAssetId,
    SkillSlot slot,
    string name) {
    public readonly int SkillId = skillId;
    public readonly int HeroAssetId = heroAssetId;
    public readonly SkillSlot Slot = slot;
    public readonly string Name = name;
  }
}
