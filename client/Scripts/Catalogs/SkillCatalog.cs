// Client-side skill names and icons

using System;
using System.Collections.Generic;
using Godot;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Assets;

namespace Meesles.Avalon;

public class SkillCatalog {
  private const string CrystalWarriorIcons = "res://Assets/Portraits/Skills/CrystalWarrior/";
  private const string HairyWizardIcons = "res://Assets/Portraits/Skills/AllHairWizard/";
  private const string SnailheadIcons = "res://Assets/Portraits/Skills/SnailHeads/";
  private const string PickleKnightIcons = "res://Assets/Portraits/Skills/PickleKnights/";

  public static readonly SkillDef[] SkillDefs = [
    new(AssetIds.SkillHairyWizardPrimary, AssetIds.HeroHairyWizard, SkillSlot.Primary, "Hairball",
      HairyWizardIcons + "skill-hairball.webp"),
    new(AssetIds.SkillHairyWizardSecondary, AssetIds.HeroHairyWizard, SkillSlot.Secondary, "Strangle",
      HairyWizardIcons + "skill-strangle.webp"),
    new(AssetIds.SkillHairyWizardTertiary, AssetIds.HeroHairyWizard, SkillSlot.Tertiary, "Close Shave",
      HairyWizardIcons + "skill-close-shave.webp"),
    new(AssetIds.SkillHairyWizardUltimate, AssetIds.HeroHairyWizard, SkillSlot.Ultimate, "Bad Hair Day",
      HairyWizardIcons + "skill-bad-hair-day.webp"),
    new(AssetIds.SkillSnailheadPrimary, AssetIds.HeroSnailhead, SkillSlot.Primary, "Venomous Slobber",
      SnailheadIcons + "skill-venomous-slobber.webp"),
    new(AssetIds.SkillSnailheadSecondary, AssetIds.HeroSnailhead, SkillSlot.Secondary, "Snail Trail",
      SnailheadIcons + "skill-snail-trail.webp"),
    new(AssetIds.SkillSnailheadTertiary, AssetIds.HeroSnailhead, SkillSlot.Tertiary, "Swivel Eyes",
      SnailheadIcons + "skill-swivel-eyes.webp"),
    new(AssetIds.SkillSnailheadUltimate, AssetIds.HeroSnailhead, SkillSlot.Ultimate, "Molt",
      SnailheadIcons + "skill-molt.webp"),
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
    new(AssetIds.SkillPickleKnightPrimary, AssetIds.HeroPickleKnight, SkillSlot.Primary, "Slip 'n Slide",
      PickleKnightIcons + "skill-slip-n-slide.webp"),
    new(AssetIds.SkillPickleKnightSecondary, AssetIds.HeroPickleKnight, SkillSlot.Secondary, "Double Dip",
      PickleKnightIcons + "skill-double-dip.webp"),
    new(AssetIds.SkillPickleKnightTertiary, AssetIds.HeroPickleKnight, SkillSlot.Tertiary, "Refresh",
      PickleKnightIcons + "skill-refresh.webp"),
    new(AssetIds.SkillPickleKnightUltimate, AssetIds.HeroPickleKnight, SkillSlot.Ultimate, "Exploosion",
      PickleKnightIcons + "skill-exploosion.webp")
  ];

  private readonly Dictionary<int, SkillDef> _byId = new();
  private readonly Dictionary<int, Texture2D> _icons = new();

  private SkillCatalog(IEnumerable<SkillDef> entries) {
    foreach (var e in entries)
      _byId[e.SkillId] = e;

    PreloadIcons(); // Fail if an authored icon is missing
  }

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

  public bool TryResolve(int skillId, out SkillDef entry) {
    return _byId.TryGetValue(skillId, out entry);
  }

  public Texture2D ResolveIcon(int skillId) { // Returns null when no icon is registered
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
    int heroStatsAssetId,
    SkillSlot slot,
    string name,
    string iconTexturePath = null) {
    public readonly int SkillId = skillId;
    public readonly int HeroStatsAssetId = heroStatsAssetId;
    public readonly SkillSlot Slot = slot;
    public readonly string Name = name;
    public readonly string IconTexturePath = iconTexturePath;
  }
}
