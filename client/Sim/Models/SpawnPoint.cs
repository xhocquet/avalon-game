using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon {
  [KlothoComponent(107)]
  [StructLayout(LayoutKind.Sequential, Pack = 4)]
  public partial struct SpawnPoint : IComponent {
    public int SpawnPointId;
    public int TeamId;
    public int LaneId;
    public int UnitTypeId;
  }
}
