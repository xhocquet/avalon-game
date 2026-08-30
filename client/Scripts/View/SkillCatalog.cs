// Client-side presentation map for hero skills.
// Skill IDs come from the sim-side ledger in sim/Assets/AssetIds.cs and match the SkillAsset rows in
// client/Sim/Data/Assets/heroes/*.json (500 range), four per hero in SkillSlot order.
// Sim owns the mechanical data (MaxRank, CooldownMs); this catalog owns the display names and icons,
// which are presentation-only and never touch the deterministic sim - the icons are not in Assets.bytes
// because the server has no use for them.

using System;
using System.Collections.Generic;
using Godot;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Assets;

namespace Meesles.Avalon;

public class SkillCatalog {
  private const string CrystalWarriorIcons = "res://Assets/Portraits/Skills/CrystalWarrior/";
  private const string HairyWizardIcons = "res://Assets/Portraits/Skills/AllHairWizard/";

  public static readonly SkillDef[] SkillDefs = [
    new(AssetIds.SkillHairyWizardPrimary, AssetIds.HeroHairyWizard, SkillSlot.Primary, "Hairball",
      HairyWizardIcons + "skill-hairball.webp"),
    new(AssetIds.SkillHairyWizardSecondary, AssetIds.HeroHairyWizard, SkillSlot.Secondary, "Strangle",
      HairyWizardIcons + "skill-strangle.webp"),
    new(AssetIds.SkillHairyWizardTertiary, AssetIds.HeroHairyWizard, SkillSlot.Tertiary, "Close Shave",
      HairyWizardIcons + "skill-close-shave.webp"),
    new(AssetIds.SkillHairyWizardUltimate, AssetIds.HeroHairyWizard, SkillSlot.Ultimate, "Bad Hair Day",
      HairyWizardIcons + "skill-bad-hair-day.webp"),
    new(AssetIds.SkillSnailheadPrimary, AssetIds.HeroSnailhead, SkillSlot.Primary, "Venomous Slobber"),
    new(AssetIds.SkillSnailheadSecondary, AssetIds.HeroSnailhead, SkillSlot.Secondary, "Snail Trail"),
    new(AssetIds.SkillSnailheadTertiary, AssetIds.HeroSnailhead, SkillSlot.Tertiary, "Swivel Eyes"),
    new(AssetIds.SkillSnailheadUltimate, AssetIds.HeroSnailhead, SkillSlot.Ultimate, "Molt"),
    new(AssetIds.SkillCrystalGiantPrimary, AssetIds.HeroCrystalGiant, SkillSlot.Primary, "Spiky Punch",
      CrystalWarriorIcons + "spiky-punch.webp"),
    new(AssetIds.SkillCrystalGiantSecondary, AssetIds.HeroCrystalGiant, SkillSlot.Secondary, "Harden",
      CrystalWarriorIcons + "harden.webp"),
    new(AssetIds.SkillCrystalGiantTertiary, AssetIds.HeroCrystalGiant, SkillSlot.Tertiary, "Crystal Bullets",
      CrystalWarriorIcons + "crystal-bullets.webp"),
    new(AssetIds.SkillCrystalGiantUltimate, AssetIds.HeroCrystalGiant, SkillSlot.Ultimate, "Chrysalis",
      CrystalWarriorIcons + "4-chrysalis.webp"),
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
  private readonly Dictionary<int, Texture2D> _icons = new();

  private SkillCatalog(IEnumerable<SkillDef> entries) {
    foreach (var e in entries)
      _byId[e.SkillId] = e;

    PreloadIcons(); // fail at construction if an authored path is wrong, not silently at draw time
  }

  // Loads every authored icon up front and caches it. A path that does not resolve is a build error,
  // not a runtime fallback, so throw with all offenders listed rather than degrade to slot art.
  private void PreloadIcons() {
    var failures = new List<string>();
    foreach (var def in _byId.Values) {
      if (string.IsNullOrEmpty(def.IconTexturePath)) continue;

      var texture = ResourceLoader.Exists(def.IconTexturePath) ? GD.Load<Texture2D>(def.IconTexturePath) : null;
      if (texture == null) {
        failures.Add($"{def.Name} (id {def.SkillId}) -> {def.IconTexturePath}");
        continue;
      }
      _icons[def.SkillId] = texture;
    }

    if (failures.Count > 0)
      throw new InvalidOperationException(
        "SkillCatalog icon paths that do not resolve:\n  " + string.Join("\n  ", failures));
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

  // Null only for a skill with no authored icon, so the caller falls back to its own slot art.
  // Authored icons are all loaded in PreloadIcons, so a non-empty path that misses the cache is a bug.
  public Texture2D ResolveIcon(int skillId) {
    if (_icons.TryGetValue(skillId, out var cached)) return cached;

    var path = TryResolve(skillId, out var def) ? def.IconTexturePath : null;
    if (!string.IsNullOrEmpty(path))
      throw new InvalidOperationException($"Skill icon '{path}' (id {skillId}) was not preloaded.");

    _icons[skillId] = null;
    return null;
  }

  public static SkillCatalog CreateDefault() {
    return new SkillCatalog(SkillDefs);
  }

  public readonly struct SkillDef(
    int skillId,
    int heroAssetId,
    SkillSlot slot,
    string name,
    string iconTexturePath = null) {
    public readonly int SkillId = skillId;
    public readonly int HeroAssetId = heroAssetId;
    public readonly SkillSlot Slot = slot;
    public readonly string Name = name;
    public readonly string IconTexturePath = iconTexturePath;
  }
}
