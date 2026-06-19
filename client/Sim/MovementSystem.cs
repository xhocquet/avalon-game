using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon {
  public class MovementSystem : ISystem, ICommandSystem {
    private static readonly FP64 StopDistance = FP64.FromDouble(0.15);

    public void OnCommand(ref Frame frame, ICommand command) {
      if (command is not MoveCommand m) return;

      FPVector3 target = new FPVector3(m.TargetX, FP64.Zero, m.TargetZ);
      if (m.UnitIdCount > 0) {
        ApplySelectedUnitTargets(ref frame, m, target);
        return;
      }

      ApplyLocalHeroTarget(ref frame, m.PlayerId, target);
    }

    public void Update(ref Frame frame) {
      var stats = frame.AssetRegistry.Get<PlayerStatsAsset>();
      if (stats == null) return;

      FP64 dt = FP64.FromInt(frame.DeltaTimeMs) / FP64.FromInt(1000);
      FP64 step = stats.MoveSpeed * dt;

      var filter = frame.Filter<UnitMoveTarget, TransformComponent>();
      while (filter.Next(out var entity)) {
        ref var moveTarget = ref frame.Get<UnitMoveTarget>(entity);
        ref var transform = ref frame.Get<TransformComponent>(entity);

        FPVector3 toTarget = moveTarget.Target - transform.Position;
        toTarget.y = FP64.Zero;
        FP64 dist = toTarget.magnitude;
        if (dist <= StopDistance) {
          frame.Remove<UnitMoveTarget>(entity);
          continue;
        }

        FPVector3 move = toTarget.normalized * step;
        if (step >= dist) move = toTarget;
        transform.Position = transform.Position + move;
        transform.Rotation = FP64.Atan2(move.x, move.z);
      }
    }

    private static void ApplySelectedUnitTargets(ref Frame frame, MoveCommand command, FPVector3 target) {
      var filter = frame.Filter<Unit>();
      while (filter.Next(out var entity)) {
        ref readonly var unit = ref frame.Get<Unit>(entity);
        if (!CommandIncludesUnitId(command, unit.UnitId)) continue;
        SetTarget(ref frame, entity, target);
      }
    }

    private static void ApplyLocalHeroTarget(ref Frame frame, int playerId, FPVector3 target) {
      var filter = frame.Filter<PlayerComponent>();
      while (filter.Next(out var entity)) {
        ref readonly var player = ref frame.Get<PlayerComponent>(entity);
        if (player.PlayerId != playerId) continue;
        SetTarget(ref frame, entity, target);
        return;
      }
    }

    private static bool CommandIncludesUnitId(MoveCommand command, int unitId) {
      int count = command.UnitIdCount;
      if (count > 8) count = 8;
      for (int i = 0; i < count; i++)
        if (command.GetUnitId(i) == unitId)
          return true;

      return false;
    }

    private static void SetTarget(ref Frame frame, EntityRef entity, FPVector3 target) {
      if (frame.Has<UnitMoveTarget>(entity)) {
        ref var moveTarget = ref frame.Get<UnitMoveTarget>(entity);
        moveTarget.Target = target;
      }
      else {
        frame.Add(entity, new UnitMoveTarget { Target = target });
      }
    }
  }
}
