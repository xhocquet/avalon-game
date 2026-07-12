// Client-only presentation catalog: maps a sim FactionId to its Godot scenes and display
// metadata. This is the deliberately non-deterministic half of the faction system — the sim
// only knows integer FactionIds (see sim/Assets/FactionAsset.cs and the Faction component);
// everything an artist/designer touches (models, icons, names) lives here and can change
// without recompiling or rehashing the sim. Faction ids match the FactionAsset AssetIds in
// client/Sim/Data/Assets.json.

using System.Collections.Generic;
using Godot;

namespace Meesles.Avalon;

public sealed class FactionCatalog {
  public const int FactionHairyWizardsId = 200;
  public const int FactionShroomsId = 201;
  public const int DefaultFactionId = FactionHairyWizardsId;

  public static readonly Def[] Defs = {
    new(FactionHairyWizardsId, "Hairy Wizards",
      "res://Scenes/Heroes/HairyWizardsHero.tscn", "res://Scenes/Mobs/SwirlyEye.tscn"),
    new(FactionShroomsId, "Shrooms",
      "res://Scenes/Heroes/snailhead.tscn", "res://Scenes/Mobs/spikeysnail.tscn")
  };

  private readonly Dictionary<int, Entry> _byId = new();
  private readonly Entry _fallback;

  private FactionCatalog(IEnumerable<Entry> entries, Entry fallback) {
    _fallback = fallback;
    foreach (var e in entries)
      _byId[e.FactionId] = e;
  }

  public IReadOnlyCollection<Entry> Entries => _byId.Values;

  public Entry Resolve(int factionId) {
    return _byId.TryGetValue(factionId, out var entry) ? entry : _fallback;
  }

  // Loads the scenes for the roster. Called wherever the view factory / prewarm needs scenes
  // (in-game only — the lobby uses Defs directly to avoid loading models).
  public static FactionCatalog CreateDefault() {
    var entries = new List<Entry>();
    foreach (var def in Defs)
      entries.Add(new Entry {
        FactionId = def.Id,
        DisplayName = def.Name,
        HeroScene = GD.Load<PackedScene>(def.HeroScenePath),
        MinionScene = GD.Load<PackedScene>(def.MinionScenePath)
      });

    var fallback = new Entry {
      FactionId = 0,
      DisplayName = "Unknown",
      HeroScene = GD.Load<PackedScene>("res://Scenes/Dummy.tscn"),
      MinionScene = GD.Load<PackedScene>("res://Scenes/Mobs/SwirlyEye.tscn")
    };
    return new FactionCatalog(entries, fallback);
  }

  // Single source of truth for the roster. The lobby reads (Id, Name) for its picker without
  // loading any models; CreateDefault() loads the scenes for the in-game view factory.
  public readonly struct Def {
    public readonly int Id;
    public readonly string Name;
    public readonly string HeroScenePath;
    public readonly string MinionScenePath;

    public Def(int id, string name, string heroScenePath, string minionScenePath) {
      Id = id;
      Name = name;
      HeroScenePath = heroScenePath;
      MinionScenePath = minionScenePath;
    }
  }

  public sealed class Entry {
    public string DisplayName;
    public int FactionId;
    public PackedScene HeroScene;
    public PackedScene MinionScene;
  }
}
