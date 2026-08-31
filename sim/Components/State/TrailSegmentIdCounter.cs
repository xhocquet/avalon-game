using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// Trail segments need ids so a client can pair the spawn event with the decal it drew and let it
// fade. Shared by every skill that lays a trail, so the ids never collide across casters.
[KlothoComponent(ComponentIds.TrailSegmentIdCounter)]
[KlothoSingletonComponent]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct TrailSegmentIdCounter : IComponent, IIdCounter {
  public int NextTrailSegmentId;

  public int NextId {
    readonly get => NextTrailSegmentId;
    set => NextTrailSegmentId = value;
  }
}
