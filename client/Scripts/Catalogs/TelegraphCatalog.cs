// Client-side telegraph presentation by skill
// Skills omitted here draw no telegraph

using System.Collections.Generic;
using Meesles.Avalon.Sim.Assets;

namespace Meesles.Avalon;

public class TelegraphCatalog {
  private const string SelfFamily = "res://Scenes/FX/Telegraphs/telegraph_family_self.tres";
  private const string HostileFamily = "res://Scenes/FX/Telegraphs/telegraph_family_hostile.tres";

  public static readonly TelegraphDef[] TelegraphDefs = [
    new(AssetIds.SkillCrystalGiantTertiary, SelfFamily, HostileFamily, 4f),
    new(AssetIds.SkillCrystalGiantUltimate, SelfFamily, HostileFamily, 4f),
    new(AssetIds.SkillHairyWizardPrimary, SelfFamily, HostileFamily, 4f),
    new(AssetIds.SkillHairyWizardSecondary, SelfFamily, HostileFamily, 4f),
    new(AssetIds.SkillHairyWizardUltimate, SelfFamily, HostileFamily, 4f),
    new(AssetIds.SkillSnailheadPrimary, SelfFamily, HostileFamily, 4f, 0.35f),
    // One circle per trail segment; its spawn event supplies FillSeconds
    new(AssetIds.SkillSnailheadSecondary, SelfFamily, HostileFamily, 4f),
    new(AssetIds.SkillSnailheadUltimate, SelfFamily, HostileFamily, 4f),
    new(AssetIds.SkillPickleKnightPrimary, SelfFamily, HostileFamily, 4f)
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
    float height,
    float fillSeconds = 0f) {
    public readonly int SkillAssetId = skillAssetId;

    public readonly string OwnFamilyPath = ownFamilyPath;
    public readonly string HostileFamilyPath = hostileFamilyPath;

    public readonly float Height = height; // Vertical half-extent

    public readonly float FillSeconds = fillSeconds; // Sweep duration when no speed is provided
  }
}
