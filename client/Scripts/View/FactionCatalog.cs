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
      "res://Scenes/Heroes/HairyWizardsHero.tscn",
      "res://Scenes/Mobs/SwirlyEye.tscn",
      "res://Assets/Portraits/allhairwizard.png"),
    new(FactionShroomsId, "Shrooms",
      "res://Scenes/Heroes/snailhead.tscn",
      "res://Scenes/Mobs/spikeysnail.tscn",
      "res://Assets/Portraits/snailhead.png")
  ];

  private readonly Dictionary<int, FactionData> _byId = new();
  private readonly FactionData _fallback;

  private FactionCatalog(IEnumerable<FactionData> entries, FactionData fallback) {
    _fallback = fallback;
    foreach (var e in entries)
      _byId[e.FactionId] = e;
  }

  public IReadOnlyCollection<FactionData> Entries => _byId.Values;

  public FactionData Resolve(int factionId) {
    return _byId.GetValueOrDefault(factionId, _fallback);
  }

  // Loads the scenes for the roster. Called wherever the view factory / prewarm needs scenes
  // (in-game only — the lobby uses Defs directly to avoid loading models).
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

    var fallback = new FactionData {
      FactionId = 0,
      DisplayName = "Unknown",
      HeroScene = GD.Load<PackedScene>("res://Scenes/Dummy.tscn"),
      MinionScene = GD.Load<PackedScene>("res://Scenes/Mobs/SwirlyEye.tscn"),
      PortraitTexture = null
    };
    return new FactionCatalog(entries, fallback);
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
