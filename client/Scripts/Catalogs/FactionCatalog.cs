// Loads and manages Faction data
// Faction IDs come from the sim-side ledger in sim/Assets/AssetIds.cs and match the
// FactionAsset rows in client/Sim/Data/Assets/heroes/*.json

using System.Collections.Generic;
using Godot;
using Meesles.Avalon.Sim.Assets;

namespace Meesles.Avalon;

public class FactionCatalog {
  public const int FactionHairyWizardsId = AssetIds.FactionHairyWizards;
  public const int FactionSnailheadsId = AssetIds.FactionSnailheads;
  public const int FactionCrystalWarriorsId = AssetIds.FactionCrystalWarriors;
  public const int FactionSkinwalkerTribeId = AssetIds.FactionSkinwalkerTribe;
  public const int FactionPickleKnightsId = AssetIds.FactionPickleKnights;
  public const int DefaultFactionId = FactionHairyWizardsId;

  public static readonly FactionDef[] FactionDefs = [
    new(FactionHairyWizardsId, "Hairy Wizards",
      "res://Scenes/Heroes/AllHairWizard.tscn",
      "res://Scenes/Mobs/SwirlyEye.tscn",
      "res://Assets/Portraits/Factions/AllHairWizards/hero.webp",
      "res://Assets/Portraits/Factions/AllHairWizards/minion.webp"),
    new(FactionSnailheadsId, "Snailheads",
      "res://Scenes/Heroes/SnailHead.tscn",
      "res://Scenes/Mobs/DeathSnail.tscn",
      "res://Assets/Portraits/Factions/Snailheads/hero.webp",
      "res://Assets/Portraits/Factions/Snailheads/minion.webp"),
    new(FactionCrystalWarriorsId, "Crystal Warriors",
      "res://Scenes/Heroes/CrystalGiant.tscn",
      "res://Scenes/Mobs/CrystalTurtle.tscn",
      "res://Assets/Portraits/Factions/CrystalWarriors/hero.webp",
      "res://Assets/Portraits/Factions/CrystalWarriors/minion.webp"),
    new(FactionSkinwalkerTribeId, "Skinwalker Tribe",
      "res://Scenes/Heroes/Skinwalker.tscn",
      "res://Scenes/Mobs/PatchRat.tscn",
      "res://Assets/Portraits/Factions/Skinwalkers/hero.webp",
      "res://Assets/Portraits/Factions/Skinwalkers/minion.webp"),
    new(FactionPickleKnightsId, "Pickle Knights",
      "res://Scenes/Heroes/PickleKnight.tscn",
      "res://Scenes/Mobs/CaperCreep.tscn",
      "res://Assets/Portraits/Factions/PickleKnights/hero.webp",
      "res://Assets/Portraits/Factions/PickleKnights/minion.webp")
  ];

  private readonly Dictionary<int, FactionData> _byId = new();

  private FactionCatalog(IEnumerable<FactionData> entries) {
    foreach (var e in entries)
      _byId[e.FactionId] = e;
  }

  public IReadOnlyCollection<FactionData> Entries => _byId.Values;

  public FactionData Resolve(int factionId) {
    return _byId.TryGetValue(factionId, out var entry)
      ? entry
      : throw new KeyNotFoundException($"No faction registered for id {factionId}.");
  }

  public static FactionCatalog CreateDefault() {
    var entries = new List<FactionData>();
    foreach (var def in FactionDefs)
      entries.Add(new FactionData {
        FactionId = def.Id,
        DisplayName = def.Name,
        HeroScene = GD.Load<PackedScene>(def.HeroScenePath),
        MinionScene = GD.Load<PackedScene>(def.MinionScenePath),
        HeroPortraitTexture = GD.Load<Texture2D>(def.HeroPortraitTexturePath),
        MinionPortraitTexture = GD.Load<Texture2D>(def.MinionPortraitTexturePath)
      });

    return new FactionCatalog(entries);
  }

  public readonly struct FactionDef(
    int id,
    string name,
    string heroScenePath,
    string minionScenePath,
    string heroPortraitTexturePath,
    string minionPortraitTexturePath) {
    public readonly int Id = id;
    public readonly string Name = name;
    public readonly string HeroScenePath = heroScenePath;
    public readonly string MinionScenePath = minionScenePath;
    public readonly string HeroPortraitTexturePath = heroPortraitTexturePath;
    public readonly string MinionPortraitTexturePath = minionPortraitTexturePath;
  }

  public class FactionData {
    public string DisplayName;
    public int FactionId;
    public PackedScene HeroScene;
    public PackedScene MinionScene;
    public Texture2D HeroPortraitTexture;
    public Texture2D MinionPortraitTexture;
  }
}
