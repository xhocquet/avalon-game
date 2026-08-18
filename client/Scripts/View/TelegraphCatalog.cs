// Client-side presentation map for skill telegraphs, the sibling of SkillCatalog.
// A skill with no row here simply draws no telegraph.
//
// Nothing about the *shape* lives here: lane count, spacing, length, width and muzzle offset are read
// off the skill's SkillAsset row at cast time, and the fill duration is that row's range/speed, so the
// bars sweep at the speed the bullets actually travel. This catalog owns only what the sim has no
// opinion on — which family (colour/effect set) each side sees, and how tall the decal box is.

using System.Collections.Generic;
using Meesles.Avalon.Sim.Assets;

namespace Meesles.Avalon;

public class TelegraphCatalog {
  private const string SelfFamily = "res://Scenes/FX/Telegraphs/telegraph_family_self.tres";
  private const string HostileFamily = "res://Scenes/FX/Telegraphs/telegraph_family_hostile.tres";

  public static readonly TelegraphDef[] TelegraphDefs = [
    new(AssetIds.SkillCrystalGiantTertiary, SelfFamily, HostileFamily, 4f),
    new(AssetIds.SkillHairyWizardPrimary, SelfFamily, HostileFamily, 4f)
  ];

  private readonly Dictionary<int, TelegraphDef> _bySkillAssetId = new();

  private TelegraphCatalog(IEnumerable<TelegraphDef> entries) {
    foreach (var e in entries)
      _bySkillAssetId[e.SkillAssetId] = e;
  }

  public bool TryResolve(int skillAssetId, out TelegraphDef entry) {
    return _bySkillAssetId.TryGetValue(skillAssetId, out entry);
  }

  public static TelegraphCatalog CreateDefault() {
    return new TelegraphCatalog(TelegraphDefs);
  }

  public readonly struct TelegraphDef(
    int skillAssetId,
    string ownFamilyPath,
    string hostileFamilyPath,
    float laneHeight) {
    public readonly int SkillAssetId = skillAssetId;

    // Family the caster's own team sees vs. what everyone else sees. A helpful skill can point both at
    // the same resource; a harmful one reads red to the people it is about to land on.
    public readonly string OwnFamilyPath = ownFamilyPath;
    public readonly string HostileFamilyPath = hostileFamilyPath;

    // Vertical half-extent of each lane box. Purely a decal concern: it has to clear the terrain the
    // lane crosses, and nothing in the sim is measured against it.
    public readonly float LaneHeight = laneHeight;
  }
}
