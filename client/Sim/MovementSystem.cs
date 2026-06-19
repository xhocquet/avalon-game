using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon {
  public class MovementSystem : ISystem, ICommandSystem {
    public void OnCommand(ref Frame frame, ICommand command) {
      if (command is not MoveCommand m) return;
      int pid = m.PlayerId;
      var filter = frame.Filter<PlayerComponent>();
      while (filter.Next(out var entity)) {
        ref var p = ref frame.Get<PlayerComponent>(entity);
        if (p.PlayerId != pid) continue;
        p.LastInputH = m.H;
        p.LastInputV = m.V;
        return;
      }
    }

    public void Update(ref Frame frame) {
      var stats = frame.AssetRegistry.Get<PlayerStatsAsset>();
      FP64 dt = FP64.FromInt(frame.DeltaTimeMs) / FP64.FromInt(1000);
      var filter = frame.Filter<PlayerComponent, TransformComponent>();
      while (filter.Next(out var entity)) {
        ref var p = ref frame.Get<PlayerComponent>(entity);
        ref var transform = ref frame.Get<TransformComponent>(entity);
        transform.Position.x += p.LastInputH * stats.MoveSpeed * dt;
        transform.Position.z += p.LastInputV * stats.MoveSpeed * dt;
      }
    }
  }
}
