using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Assets;

// Multi-instance catalog asset: one instance per faction, keyed by its own AssetId
// (the faction id used everywhere — selection command, Faction component, client catalog).
// No AssetId or Key named arg on the attribute: both are singleton-lookup keys, and a catalog
// has many instances of this one type. Callers fetch a specific faction via Get<FactionAsset>(
// factionId). Faction ids live in the 200 range to stay clear of the singleton assets
// (PlayerStats 100 / WaveRules 101 / MinionStats 103).
//
// Only sim-relevant data belongs here (determinism-safe, loaded identically on all peers).
// Presentation (scenes, icons, display names) lives client-side in FactionCatalog.
[KlothoDataAsset(104)]
public partial class FactionAsset : IDataAsset {
  // --- Champion (hero) pointer ---
  // Archetype id for this faction's hero champion. HeroSpawnSystem stamps it onto the hero's
  // Unit.UnitTypeId; the client view can key off it for champion-specific scenes/abilities.
  [KlothoOrder(0)] public int ChampionUnitTypeId;

  // Pointer into the MinionStats catalog (a MinionStatsAsset AssetId) for this faction's minion
  // combat stats. Points at the shared MinionStats (103) until each faction gets its own.
  [KlothoOrder(2)] public int MinionStatsAssetId;

  // --- Minion pointers ---
  // Archetype id for this faction's wave minions (Unit.UnitTypeId). Lets a faction field a
  // distinct minion type; defaults to the shared minion type until factions diverge.
  [KlothoOrder(1)] public int MinionUnitTypeId;
}
