using System.Collections.Generic;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Models;
using Meesles.Avalon.Sim.Navigation;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Deterministic.Navigation;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon {
  public sealed class NavigationAgentSystem : ISystem {
    private static readonly FP64 AvoidanceGridCellSize = FP64.FromInt(5);
    private static readonly FP64 SnapThresholdSqr = FP64.FromDouble(0.01);
    private static readonly FP64 FlowFieldArrivalDistSqr = FP64.FromDouble(0.09); // 0.3 units (matches WaypointThreshold)
    private static readonly FP64 FlowFieldDirectSteerDistSqr = FP64.FromDouble(4.0); // 2.0 units — switch to direct steering
    private static readonly FP64 MinionNeighborDist = FP64.FromInt(2);

    private readonly NavigationRuntime _navigation;

    // Temporal spreading: only update a fraction of agents per tick for expensive phases.
    // 1 = every tick (no spreading), 2 = every other tick, etc.
    public int HeroSteeringSpread = 1;
    public int MinionSteeringSpread = 1;
    public int AvoidanceSpread = 1;

    // Separate collision layers
    private readonly SpatialHashGrid _minionAvoidanceGrid = new(AvoidanceGridCellSize);
    private readonly SpatialHashGrid _heroAvoidanceGrid = new(AvoidanceGridCellSize);
    private readonly List<EntityRef> _nearbyAgents = new();

    // Hero entities use existing A* path
    private EntityRef[] _heroEntities = new EntityRef[16];
    private int _heroCount;

    // Minion entities use flow fields
    private EntityRef[] _minionEntities = new EntityRef[256];
    private int _minionCount;

    // Spread-subset arrays for steering/avoidance phases
    private EntityRef[] _heroSubset = new EntityRef[16];
    private EntityRef[] _minionSubset = new EntityRef[256];
    private EntityRef[] _avoidanceSubset = new EntityRef[128];

    // Shared position-sync bookkeeping
    private EntityRef[] _allEntities = new EntityRef[128];
    private FPVector3[] _lastSnappedPositions = new FPVector3[128];
    private int _allCount;

    public NavigationAgentSystem(NavigationRuntime navigation) {
      _navigation = navigation;
    }

    public void Update(ref Frame frame) {
      _heroCount = 0;
      _minionCount = 0;
      _allCount = 0;

      FP64 dt = FP64.FromInt(frame.DeltaTimeMs) / FP64.FromInt(1000);

      // Phase 1: Collect and categorize all nav agents
      var filter = frame.Filter<NavAgentComponent, TransformComponent>();
      while (filter.Next(out var entity)) {
        // Dead units awaiting respawn are frozen at their spawn point by
        // RespawnSystem. Exclude them entirely so navigation neither snaps
        // their transform onto the navmesh nor lets them push living units.
        if (frame.Has<PendingRespawn>(entity))
          continue;

        ref var nav = ref frame.Get<NavAgentComponent>(entity);
        ref readonly var transform = ref frame.GetReadOnly<TransformComponent>(entity);

        EnsureAllCapacity(_allCount + 1);
        SyncAgentPosition(ref nav, transform.Position, _allCount);
        _allEntities[_allCount++] = entity;

        bool isMinion = frame.Has<Minion>(entity);

        if (isMinion && frame.Has<UnitMoveTarget>(entity)) {
          EnsureMinionCapacity(_minionCount + 1);
          _minionEntities[_minionCount++] = entity;
        } else {
          if (frame.Has<UnitMoveTarget>(entity)) {
            ref readonly var target = ref frame.GetReadOnly<UnitMoveTarget>(entity);
            if (DestinationChanged(in nav, target.Target))
              NavAgentComponent.SetDestination(ref nav, target.Target);
          } else if (nav.HasNavDestination) {
            NavAgentComponent.Stop(ref nav);
          }

          EnsureHeroCapacity(_heroCount + 1);
          _heroEntities[_heroCount++] = entity;
        }
      }

      if (_allCount == 0)
        return;

      // Phase 2: Hero pathfinding via existing A* + funnel (spread across ticks)
      if (_heroCount > 0) {
        int heroSubsetCount = BuildSpreadSubset(
          _heroEntities, _heroCount, HeroSteeringSpread, frame.Tick, 0,
          ref _heroSubset);
        if (heroSubsetCount > 0)
          _navigation.AgentSystem.UpdateSteering(ref frame, _heroSubset, heroSubsetCount, frame.Tick);
      }

      // Phase 3: Minion steering via flow fields (spread across ticks)
      {
        int minionSubsetCount = BuildSpreadSubset(
          _minionEntities, _minionCount, MinionSteeringSpread, frame.Tick, 1,
          ref _minionSubset);
        if (minionSubsetCount > 0)
          UpdateMinionFlowFieldSteering(ref frame, _minionSubset, minionSubsetCount);
      }

      // Phase 4: ORCA avoidance with separate collision layers (spread across ticks)
      var avoidance = _navigation.Avoidance;
      if (avoidance != null) {
        _minionAvoidanceGrid.Clear();
        _heroAvoidanceGrid.Clear();

        for (int i = 0; i < _allCount; i++) {
          var entity = _allEntities[i];
          ref var nav = ref frame.Get<NavAgentComponent>(entity);
          FPVector2 posXZ = nav.Position.ToXZ();

          if (frame.Has<Minion>(entity))
            _minionAvoidanceGrid.Insert(entity, posXZ);
          else
            _heroAvoidanceGrid.Insert(entity, posXZ);
        }

        int avoidSubsetCount = BuildSpreadSubset(
          _allEntities, _allCount, AvoidanceSpread, frame.Tick, 2,
          ref _avoidanceSubset);

        for (int i = 0; i < avoidSubsetCount; i++) {
          var entity = _avoidanceSubset[i];
          ref var nav = ref frame.Get<NavAgentComponent>(entity);
          if (nav.Status != (byte)FPNavAgentStatus.Moving)
            continue;

          bool isMinion = frame.Has<Minion>(entity);
          var grid = isMinion ? _minionAvoidanceGrid : _heroAvoidanceGrid;
          FP64 neighborDist = isMinion ? MinionNeighborDist : avoidance.NeighborDist;
          grid.QueryRadius(nav.Position.ToXZ(), neighborDist, _nearbyAgents);
          nav.DesiredVelocity = avoidance.ComputeNewVelocity(entity, ref frame, _nearbyAgents, dt);
        }
      }

      // Phase 5: Movement integration (all agents)
      _navigation.AgentSystem.UpdateMovement(ref frame, _allEntities, _allCount, dt);

      // Phase 6: Sync back to transforms + arrival detection
      for (int i = 0; i < _allCount; i++) {
        var entity = _allEntities[i];
        ref var nav = ref frame.Get<NavAgentComponent>(entity);
        ref var transform = ref frame.Get<TransformComponent>(entity);

        transform.Position = nav.Position;
        if (nav.Velocity.sqrMagnitude > FP64.Zero)
          transform.Rotation = FP64.Atan2(nav.Velocity.x, nav.Velocity.y);

        if (nav.Status == (byte)FPNavAgentStatus.Arrived)
          frame.Remove<UnitMoveTarget>(entity);
      }
    }

    private void UpdateMinionFlowFieldSteering(ref Frame frame, EntityRef[] entities, int count) {
      var query = _navigation.Query;
      var flowFields = _navigation.FlowFields;

      for (int i = 0; i < count; i++) {
        var entity = entities[i];
        ref var nav = ref frame.Get<NavAgentComponent>(entity);
        ref readonly var moveTarget = ref frame.GetReadOnly<UnitMoveTarget>(entity);

        FPVector2 goalXZ = new FPVector2(moveTarget.Target.x, moveTarget.Target.z);
        FPVector2 agentXZ = nav.Position.ToXZ();
        FPVector2 toTargetXZ = goalXZ - agentXZ;
        FP64 distSqr = toTargetXZ.sqrMagnitude;

        // Arrival check (same threshold as Klotho WaypointThreshold)
        if (distSqr <= FlowFieldArrivalDistSqr) {
          nav.Status = (byte)FPNavAgentStatus.Arrived;
          nav.Velocity = FPVector2.Zero;
          nav.DesiredVelocity = FPVector2.Zero;
          continue;
        }

        // When close to target, steer directly regardless of flow field
        if (distSqr <= FlowFieldDirectSteerDistSqr) {
          FP64 mag = FP64.Sqrt(distSqr);
          nav.DesiredVelocity = (toTargetXZ / mag) * nav.Speed;
          nav.Status = (byte)FPNavAgentStatus.Moving;
          continue;
        }

        // Resolve goal triangle for flow field lookup
        int goalTri = query.FindTriangle(goalXZ);
        if (goalTri < 0) {
          nav.Status = (byte)FPNavAgentStatus.Moving;
          FP64 mag = FP64.Sqrt(distSqr);
          nav.DesiredVelocity = (toTargetXZ / mag) * nav.Speed;
          continue;
        }

        // Get or create flow field for this destination
        var field = flowFields.GetOrCreate(goalTri);

        int currentTri = nav.CurrentTriangleIndex;
        if (currentTri < 0 || currentTri >= field.NextTriangle.Length) {
          nav.Status = (byte)FPNavAgentStatus.Moving;
          FP64 mag = FP64.Sqrt(distSqr);
          nav.DesiredVelocity = (toTargetXZ / mag) * nav.Speed;
          continue;
        }

        int next = field.NextTriangle[currentTri];
        if (next == TriangleFlowField.AT_GOAL || next == TriangleFlowField.UNREACHABLE) {
          FP64 mag = FP64.Sqrt(distSqr);
          nav.DesiredVelocity = mag > FP64.Zero ? (toTargetXZ / mag) * nav.Speed : FPVector2.Zero;
        } else {
          FP64 dist = FP64.Sqrt(distSqr);
          FPVector2 directDir = toTargetXZ / dist;
          FPVector2 blended = field.ExitDirection[currentTri] + directDir;
          FP64 blendMag = blended.magnitude;
          nav.DesiredVelocity = blendMag > FP64.Zero
            ? (blended / blendMag) * nav.Speed
            : field.ExitDirection[currentTri] * nav.Speed;
        }

        nav.Status = (byte)FPNavAgentStatus.Moving;
      }
    }

    private void SyncAgentPosition(ref NavAgentComponent nav, FPVector3 position, int slotIndex) {
      FPVector3 delta = position - _lastSnappedPositions[slotIndex];
      FP64 moveSqr = delta.x * delta.x + delta.z * delta.z;

      if (nav.CurrentTriangleIndex >= 0 && moveSqr < SnapThresholdSqr) {
        nav.Position = position;
        return;
      }

      var snapXZ = _navigation.Query.ClosestPointOnNavMesh(position.ToXZ(), out int snapTri);
      nav.Position = snapTri >= 0
        ? new FPVector3(snapXZ.x, position.y, snapXZ.y)
        : position;

      if (snapTri >= 0)
        nav.CurrentTriangleIndex = snapTri;

      _lastSnappedPositions[slotIndex] = nav.Position;
    }

    private static bool DestinationChanged(in NavAgentComponent nav, FPVector3 target) {
      return !nav.HasNavDestination
        || nav.Destination.x != target.x
        || nav.Destination.y != target.y
        || nav.Destination.z != target.z;
    }

    private void EnsureAllCapacity(int required) {
      if (required <= _allEntities.Length)
        return;
      int newSize = _allEntities.Length;
      while (newSize < required) newSize *= 2;
      System.Array.Resize(ref _allEntities, newSize);
      System.Array.Resize(ref _lastSnappedPositions, newSize);
    }

    private static int BuildSpreadSubset(
        EntityRef[] source, int count, int spread, int tick, int offset,
        ref EntityRef[] dest) {
      if (spread <= 1) {
        EnsureCapacity(ref dest, count);
        System.Array.Copy(source, dest, count);
        return count;
      }

      int bucket = ((tick - offset) % spread + spread) % spread;
      int subsetCount = 0;
      EnsureCapacity(ref dest, count);
      for (int i = 0; i < count; i++) {
        if (i % spread == bucket)
          dest[subsetCount++] = source[i];
      }
      return subsetCount;
    }

    private static void EnsureCapacity(ref EntityRef[] array, int required) {
      if (required <= array.Length)
        return;
      int newSize = array.Length;
      while (newSize < required) newSize *= 2;
      System.Array.Resize(ref array, newSize);
    }

    private void EnsureHeroCapacity(int required) {
      if (required <= _heroEntities.Length)
        return;
      int newSize = _heroEntities.Length;
      while (newSize < required) newSize *= 2;
      System.Array.Resize(ref _heroEntities, newSize);
    }

    private void EnsureMinionCapacity(int required) {
      if (required <= _minionEntities.Length)
        return;
      int newSize = _minionEntities.Length;
      while (newSize < required) newSize *= 2;
      System.Array.Resize(ref _minionEntities, newSize);
    }
  }
}
