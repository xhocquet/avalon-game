using System.Collections.Generic;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Models;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Deterministic.Navigation;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon {
  public sealed class NavigationAgentSystem : ISystem {
    private static readonly FP64 AvoidanceGridCellSize = FP64.FromInt(5);

    private readonly NavigationRuntime _navigation;
    private readonly SpatialHashGrid _avoidanceGrid = new(AvoidanceGridCellSize);
    private readonly List<EntityRef> _nearbyAgents = new();
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

      _navigation.AgentSystem.UpdateSteering(ref frame, _entities, count, frame.Tick);

      var avoidance = _navigation.Avoidance;
      if (avoidance != null) {
        _avoidanceGrid.Clear();
        for (int i = 0; i < count; i++) {
          ref var nav = ref frame.Get<NavAgentComponent>(_entities[i]);
          _avoidanceGrid.Insert(_entities[i], nav.Position.ToXZ());
        }

        for (int i = 0; i < count; i++) {
          ref var nav = ref frame.Get<NavAgentComponent>(_entities[i]);
          if (nav.Status == (byte)FPNavAgentStatus.Moving) {
            _avoidanceGrid.QueryRadius(nav.Position.ToXZ(), avoidance.NeighborDist, _nearbyAgents);
            nav.DesiredVelocity = avoidance.ComputeNewVelocity(
              _entities[i], ref frame, _nearbyAgents, dt);
          }
        }
      }

      _navigation.AgentSystem.UpdateMovement(ref frame, _entities, count, dt);

      for (int i = 0; i < count; i++) {
        var entity = _entities[i];
        ref var nav = ref frame.Get<NavAgentComponent>(entity);
        ref var transform = ref frame.Get<TransformComponent>(entity);

        transform.Position = nav.Position;
        if (nav.Velocity.sqrMagnitude > FP64.Zero)
          transform.Rotation = FP64.Atan2(nav.Velocity.x, nav.Velocity.y);

        if (nav.Status == (byte)FPNavAgentStatus.Arrived)
          frame.Remove<UnitMoveTarget>(entity);
      }
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
