using System.Runtime.InteropServices;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// "Snap" here = project the agent's position onto the baked navmesh (the walkable triangle
// surface), pulling it back on if it drifted off and refreshing which triangle it stands in.
//
// Where NavigationAgentSystem last snapped this agent onto the navmesh. The per-tick position sync
// skips the (relatively expensive) navmesh snap while an agent has drifted less than
// NavigationTuningAsset.PositionSnapThreshold from this point, so this is the input to that
// decision — and the decision sets nav.Position, which becomes transform.Position.
//
// Stored in frame state (not on the system) so it rolls back deterministically. A client that
// mispredicts and resimulates ticks T..T+n must re-derive the same snap/skip choice the server
// made; a copy parked in a system field would survive the rollback carrying positions from the
// discarded branch, and the replay would put the unit somewhere the server never did.
//
// Added lazily on an agent's first snap and thereafter rewritten on every snap. Keyed by entity,
// so it stays attached to its agent across spawns, deaths and respawns.
[KlothoComponent(ComponentIds.NavSnapTracker)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct NavSnapTracker : IComponent {
  public FP64 LastSnappedX;
  public FP64 LastSnappedZ;
}
