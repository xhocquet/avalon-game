using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using Meesles.Avalon.Sim.Models;
using Meesles.Avalon.Sim.Assets;

namespace Meesles.Avalon {
  // Milestone M3 (quick + dirty): march every minion straight at map center by integrating
  // its transform directly (FP64, no navmesh, no physics). Replaced by navmesh in Milestone D.
  public class MinionMoveSystem : ISystem {
    private static readonly FP64 StopRadius = FP64.One;

    public void Update(ref Frame frame) {
      var stats = frame.AssetRegistry.Get<MinionStatsAsset>();
      if (stats == null) return;

      FP64 dt = FP64.FromInt(frame.DeltaTimeMs) / FP64.FromInt(1000);
      FP64 step = stats.MoveSpeed * dt;

      var filter = frame.Filter<Minion, TransformComponent>();
      while (filter.Next(out var entity)) {
        ref var transform = ref frame.Get<TransformComponent>(entity);

        // Move toward center on the XZ plane only; keep the minion on the ground.
        FPVector3 toCenter = new FPVector3(-transform.Position.x, FP64.Zero, -transform.Position.z);
        FP64 dist = toCenter.magnitude;
        if (dist <= StopRadius) continue;

        FPVector3 move = toCenter.normalized * step;
        if (step >= dist) move = toCenter; // don't overshoot center
        transform.Position = transform.Position + move;
      }
    }
  }
}
