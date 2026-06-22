using xpTURN.Klotho.Core;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Commands {
  [KlothoSerializable(103)]
  public partial class AttackCommand : CommandBase {
    public override bool IsContinuousInput => false;

    [KlothoOrder(0)] public int TargetUnitId;
    [KlothoOrder(1)] public int UnitIdCount;
    [KlothoOrder(2)] public int UnitId0;
    [KlothoOrder(3)] public int UnitId1;
    [KlothoOrder(4)] public int UnitId2;
    [KlothoOrder(5)] public int UnitId3;
    [KlothoOrder(6)] public int UnitId4;
    [KlothoOrder(7)] public int UnitId5;
    [KlothoOrder(8)] public int UnitId6;
    [KlothoOrder(9)] public int UnitId7;

    public int GetUnitId(int index) {
      return index switch {
        0 => UnitId0,
        1 => UnitId1,
        2 => UnitId2,
        3 => UnitId3,
        4 => UnitId4,
        5 => UnitId5,
        6 => UnitId6,
        7 => UnitId7,
        _ => 0,
      };
    }

    public void SetUnitId(int index, int unitId) {
      switch (index) {
        case 0: UnitId0 = unitId; break;
        case 1: UnitId1 = unitId; break;
        case 2: UnitId2 = unitId; break;
        case 3: UnitId3 = unitId; break;
        case 4: UnitId4 = unitId; break;
        case 5: UnitId5 = unitId; break;
        case 6: UnitId6 = unitId; break;
        case 7: UnitId7 = unitId; break;
      }
    }
  }
}
