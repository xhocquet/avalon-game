using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Assets;

// Instance ids live in the AssetIds.Faction* block; look one up with Get<FactionAsset>(id).
[KlothoDataAsset(AssetIds.TypeIds.Faction)]
public partial class FactionAsset : IDataAsset {
  [KlothoOrder(1)] public int MinionUnitTypeId;

  // Authored but never read: the hero's unit type comes from the spawn path, and minion combat
  // stats resolve through the single Get<MinionStatsAsset>() row rather than per-faction. Kept
  // commented (with their KlothoOrder slots reserved) for when factions get distinct champions or
  // their own minion stat rows — uncomment here, re-add the keys to Assets.json, and regenerate.
  // [KlothoOrder(0)] public int ChampionUnitTypeId;
  // [KlothoOrder(2)] public int MinionStatsAssetId;
}
