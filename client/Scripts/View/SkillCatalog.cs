// Client-side presentation map for hero skills.
// Skill IDs come from the sim-side ledger in sim/Assets/AssetIds.cs and match the SkillAsset rows in
// client/Sim/Data/Assets/heroes/*.json (500 range), four per hero in SkillSlot order.
// Sim owns the mechanical data (MaxRank, CooldownMs); this catalog owns the display names, which are
// presentation-only and never touch the deterministic sim.

using System.Collections.Generic;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Assets;

namespace Meesles.Avalon;

public class SkillCatalog {
  public static readonly SkillDef[] SkillDefs = [
    new(AssetIds.SkillHairyWizardPrimary, AssetIds.HeroHairyWizard, SkillSlot.Primary, "Hairball"),
    new(AssetIds.SkillHairyWizardSecondary, AssetIds.HeroHairyWizard, SkillSlot.Secondary, "Strangle"),
    new(AssetIds.SkillHairyWizardTertiary, AssetIds.HeroHairyWizard, SkillSlot.Tertiary, "Close Shave"),
    new(AssetIds.SkillHairyWizardUltimate, AssetIds.HeroHairyWizard, SkillSlot.Ultimate, "Bad Hair Day"),
    new(AssetIds.SkillShroomPrimary, AssetIds.HeroShroom, SkillSlot.Primary, "Venomous Slobber"),
    new(AssetIds.SkillShroomSecondary, AssetIds.HeroShroom, SkillSlot.Secondary, "Snail Trail"),
    new(AssetIds.SkillShroomTertiary, AssetIds.HeroShroom, SkillSlot.Tertiary, "Swivel Eyes"),
    new(AssetIds.SkillShroomUltimate, AssetIds.HeroShroom, SkillSlot.Ultimate, "Molt"),
    new(AssetIds.SkillCrystalGiantPrimary, AssetIds.HeroCrystalGiant, SkillSlot.Primary, "Spiky Punch"),
    new(AssetIds.SkillCrystalGiantSecondary, AssetIds.HeroCrystalGiant, SkillSlot.Secondary, "Harden"),
    new(AssetIds.SkillCrystalGiantTertiary, AssetIds.HeroCrystalGiant, SkillSlot.Tertiary, "Crystal Bullets"),
    new(AssetIds.SkillCrystalGiantUltimate, AssetIds.HeroCrystalGiant, SkillSlot.Ultimate, "Carbon Compression"),
    new(AssetIds.SkillSkinwalkerPrimary, AssetIds.HeroSkinwalker, SkillSlot.Primary, "Sprint"),
    new(AssetIds.SkillSkinwalkerSecondary, AssetIds.HeroSkinwalker, SkillSlot.Secondary, "Daily Practice"),
    new(AssetIds.SkillSkinwalkerTertiary, AssetIds.HeroSkinwalker, SkillSlot.Tertiary, "Eat to Survive"),
    new(AssetIds.SkillSkinwalkerUltimate, AssetIds.HeroSkinwalker, SkillSlot.Ultimate, "Desperation"),
    new(AssetIds.SkillPickleKnightPrimary, AssetIds.HeroPickleKnight, SkillSlot.Primary, "Slip 'n Slide"),
    new(AssetIds.SkillPickleKnightSecondary, AssetIds.HeroPickleKnight, SkillSlot.Secondary, "Double Dip"),
    new(AssetIds.SkillPickleKnightTertiary, AssetIds.HeroPickleKnight, SkillSlot.Tertiary, "Refresh"),
    new(AssetIds.SkillPickleKnightUltimate, AssetIds.HeroPickleKnight, SkillSlot.Ultimate, "Exploosion")
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

  // For UI that paints a slot before it knows the hero, where an unseeded id 0 is expected rather than a bug.
  public bool TryResolve(int skillId, out SkillDef entry) {
    return _byId.TryGetValue(skillId, out entry);
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
