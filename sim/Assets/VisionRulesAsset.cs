using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Assets;

[KlothoDataAsset(AssetIds.TypeIds.VisionRules, AssetId = AssetIds.VisionRules, Key = "VisionRules")]
public partial class VisionRulesAsset : IDataAsset {
  [KlothoOrder(0)] public FP64 HeroRange = FP64.FromInt(18);
  [KlothoOrder(1)] public FP64 MinionRange = FP64.FromInt(12);
  [KlothoOrder(2)] public FP64 TurretRange = FP64.FromInt(18);
  [KlothoOrder(3)] public FP64 CrystalRange = FP64.FromInt(18);

  public FP64 RangeFor(int unitTypeId) => unitTypeId switch {
    SimulationSetup.PlayerUnitTypeId => HeroRange,
    SimulationSetup.MinionUnitTypeId => MinionRange,
    SimulationSetup.TurretUnitTypeId => TurretRange,
    SimulationSetup.CrystalUnitTypeId => CrystalRange,
    _ => FP64.Zero
  };
}
