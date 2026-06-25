using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Models;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Deterministic.Navigation;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon {
  public sealed class NavigationAgentSystem : ISystem {
    private readonly NavigationRuntime _navigation;
    private EntityRef[] _entities = new EntityRef[128];

    public NavigationAgentSystem(NavigationRuntime navigation) {
      _navigation = navigation;
    }

    public void Update(ref Frame frame) {
      int count = 0;
      FP64 dt = FP64.FromInt(frame.DeltaTimeMs) / FP64.FromInt(1000);

      var filter = frame.Filter<NavAgentComponent, TransformComponent>();
      while (filter.Next(out var entity)) {
        ref var nav = ref frame.Get<NavAgentComponent>(entity);
        ref readonly var transform = ref frame.GetReadOnly<TransformComponent>(entity);

        SyncAgentPosition(ref nav, transform.Position);
        if (frame.Has<UnitMoveTarget>(entity)) {
          ref readonly var target = ref frame.GetReadOnly<UnitMoveTarget>(entity);
          if (DestinationChanged(in nav, target.Target))
            NavAgentComponent.SetDestination(ref nav, target.Target);
        }
        else if (nav.HasNavDestination) {
          NavAgentComponent.Stop(ref nav);
        }

        EnsureCapacity(count + 1);
        _entities[count++] = entity;
      }

      if (count == 0)
        return;

      _navigation.AgentSystem.Update(ref frame, _entities, count, frame.Tick, dt);

      for (int i = 0; i < count; i++) {
        var entity = _entities[i];
        ref var nav = ref frame.Get<NavAgentComponent>(entity);
        ref var transform = ref frame.Get<TransformComponent>(entity);

        transform.Position = nav.Position;
        if (nav.Velocity.sqrMagnitude > FP64.Zero)
          transform.Rotation = FP64.Atan2(nav.Velocity.x, nav.Velocity.y);

        if (ShouldStop(ref frame, entity, in nav))
          frame.Remove<UnitMoveTarget>(entity);
      }
    }

    private bool ShouldStop(ref Frame frame, EntityRef entity, in NavAgentComponent nav) {
      if (nav.Status == (byte)FPNavAgentStatus.Arrived)
        return true;

      if (!frame.Has<UnitMoveTarget>(entity))
        return false;

      ref readonly var target = ref frame.GetReadOnly<UnitMoveTarget>(entity);
      int sameTargetCount = CountAgentsWithTarget(ref frame, target.Target);
      int nearbyAgentCount = CountAgentsNearTarget(ref frame, target.Target, nav.Radius * FP64.FromInt(4));
      if (sameTargetCount <= 1 && nearbyAgentCount <= 1)
        return false;

      FP64 arrivalRadius = GetSharedTargetArrivalRadius(in nav, sameTargetCount > nearbyAgentCount
        ? sameTargetCount
        : nearbyAgentCount);
      FPVector2 toTarget = target.Target.ToXZ() - nav.Position.ToXZ();
      return toTarget.sqrMagnitude <= arrivalRadius * arrivalRadius;
    }

    private int CountAgentsWithTarget(ref Frame frame, FPVector3 target) {
      int count = 0;
      var filter = frame.Filter<NavAgentComponent, UnitMoveTarget>();
      while (filter.Next(out var entity)) {
        ref readonly var moveTarget = ref frame.GetReadOnly<UnitMoveTarget>(entity);
        if (moveTarget.Target.x == target.x && moveTarget.Target.y == target.y && moveTarget.Target.z == target.z)
          count++;
      }

      return count;
    }

    private int CountAgentsNearTarget(ref Frame frame, FPVector3 target, FP64 radius) {
      int count = 0;
      FP64 radiusSqr = radius * radius;
      var filter = frame.Filter<NavAgentComponent>();
      while (filter.Next(out var entity)) {
        ref readonly var nav = ref frame.GetReadOnly<NavAgentComponent>(entity);
        FPVector2 toTarget = target.ToXZ() - nav.Position.ToXZ();
        if (toTarget.sqrMagnitude <= radiusSqr)
          count++;
      }

      return count;
    }

    private static FP64 GetSharedTargetArrivalRadius(in NavAgentComponent nav, int sameTargetCount) {
      FP64 spread = nav.Radius * FP64.Sqrt(FP64.FromInt(sameTargetCount));
      FP64 minSpread = nav.Radius * FP64.FromInt(2);
      if (spread < minSpread)
        spread = minSpread;

      return spread > nav.StoppingDistance ? spread : nav.StoppingDistance;
    }

    private void SyncAgentPosition(ref NavAgentComponent nav, FPVector3 position) {
      var snapXZ = _navigation.Query.ClosestPointOnNavMesh(position.ToXZ(), out int snapTri);
      nav.Position = snapTri >= 0
        ? new FPVector3(snapXZ.x, position.y, snapXZ.y)
        : position;

      if (snapTri >= 0)
        nav.CurrentTriangleIndex = snapTri;
    }

    private static bool DestinationChanged(in NavAgentComponent nav, FPVector3 target) {
      return !nav.HasNavDestination
        || nav.Destination.x != target.x
        || nav.Destination.y != target.y
        || nav.Destination.z != target.z;
    }

    private void EnsureCapacity(int required) {
      if (required <= _entities.Length)
        return;

      int newSize = _entities.Length;
      while (newSize < required)
        newSize *= 2;

      System.Array.Resize(ref _entities, newSize);
    }
  }
}
