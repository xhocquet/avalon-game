using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Assets;

// Instance ids live in the AssetIds.Skill* block; look one up with Get<SkillAsset>(id). A hero names
// its four rows through HeroAsset.Skill1..4AssetId, and HeroFactory copies those ids onto the
// hero's SkillsComponent at spawn.
//
// Deliberately thin: this is the row per-rank tuning grows into once a skill does something. The
// effect itself lives in code, in the hero's folder under Heroes/.
[KlothoDataAsset(AssetIds.TypeIds.Skill)]
public partial class SkillAsset : IDataAsset {
  [KlothoOrder(0)] public int MaxRank;

  // Authored in milliseconds so it reads the same whatever the tick rate is; SkillActions converts it
  // to ticks once, at cast time.
  [KlothoOrder(1)] public int CooldownMs;
}
