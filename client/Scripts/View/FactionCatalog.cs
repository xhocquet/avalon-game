// Loads and manages Faction data
// Faction IDs match the FactionAsset.AssetIds in client/Sim/Data/Assets.json

using System.Collections.Generic;
using Godot;

namespace Meesles.Avalon;

public sealed class FactionCatalog {
  public const int FactionHairyWizardsId = 200;
  public const int FactionShroomsId = 201;
  public const int DefaultFactionId = FactionHairyWizardsId;

  public static readonly FactionDef[] FactionDefs = [
    new(FactionHairyWizardsId, "Hairy Wizards",
      "res://Scenes/Heroes/AllHairWizard.tscn",
      "res://Scenes/Mobs/SwirlyEye.tscn",
      "res://Assets/Portraits/AllHairWizard.png"),
    new(FactionShroomsId, "Shrooms",
      "res://Scenes/Heroes/SnailHead.tscn",
      "res://Scenes/Mobs/DeathSnail.tscn",
      "res://Assets/Portraits/SnailHead.png")
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
        PortraitTexture = GD.Load<Texture2D>(def.PortraitTexturePath)
      });

    return new FactionCatalog(entries);
  }

  public readonly struct FactionDef(
    int id,
    string name,
    string heroScenePath,
    string minionScenePath,
    string portraitTexturePath) {
    public readonly int Id = id;
    public readonly string Name = name;
    public readonly string HeroScenePath = heroScenePath;
    public readonly string MinionScenePath = minionScenePath;
    public readonly string PortraitTexturePath = portraitTexturePath;
  }

  public sealed class FactionData {
    public string DisplayName;
    public int FactionId;
    public PackedScene HeroScene;
    public PackedScene MinionScene;
    public Texture2D PortraitTexture;
  }
}
