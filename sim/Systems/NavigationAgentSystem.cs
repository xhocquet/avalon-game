using System;
using System.Collections.Generic;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using Meesles.Avalon.Sim.Navigation;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Deterministic.Navigation;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon;

// All steering/settle/spread tuning lives in NavigationTuningAsset (Assets/rules.json). Squared
// distances are derived once per tick from the linear values authored there.
public class NavigationAgentSystem : ISystem {
  private readonly NavigationRuntime _navigation;
  private readonly List<EntityRef> _nearbyAgents = new();
  private int _allCount;

  // Separate collision layers. Built on first use: the cell size comes from the tuning asset,
  // which isn't available at construction time.
  private SpatialHashGrid _heroAvoidanceGrid;
  private SpatialHashGrid _minionAvoidanceGrid;

  // Shared position-sync bookkeeping
  private EntityRef[] _allEntities = new EntityRef[128];
  private EntityRef[] _avoidanceSubset = new EntityRef[128];
  private int _heroCount;

  // Hero entities use existing A* path
  private EntityRef[] _heroEntities = new EntityRef[16];

  // Spread-subset arrays for steering/avoidance phases
  private EntityRef[] _heroSubset = new EntityRef[16];
  private int _minionCount;

  // Minion entities use flow fields
  private EntityRef[] _minionEntities = new EntityRef[256];
  private EntityRef[] _minionSubset = new EntityRef[256];

  public NavigationAgentSystem(NavigationRuntime navigation) {
    _navigation = navigation;
  }

  public void Update(ref Frame frame) {
    var tuning = frame.AssetRegistry.Get<NavigationTuningAsset>();

    _heroAvoidanceGrid ??= new SpatialHashGrid(tuning.AvoidanceGridCellSize);
    _minionAvoidanceGrid ??= new SpatialHashGrid(tuning.AvoidanceGridCellSize);

    _heroCount = 0;
    _minionCount = 0;
    _allCount = 0;

    var dt = FP64.FromInt(frame.DeltaTimeMs) / FP64.FromInt(1000);
    var snapThresholdSqr = tuning.PositionSnapThreshold * tuning.PositionSnapThreshold;

    // Phase 1: Collect and categorize all nav agents
    var filter = frame.FilterWithout<NavAgentComponent, TransformComponent, PendingRespawn>();
    while (filter.Next(out var entity)) {
      ref var nav = ref frame.Get<NavAgentComponent>(entity);
      ref readonly var transform = ref frame.GetReadOnly<TransformComponent>(entity);

      EnsureCapacity(ref _allEntities, _allCount + 1);
      SyncAgentPosition(ref frame, entity, ref nav, transform.Position, snapThresholdSqr);

      if (frame.Has<StatsComponent>(entity))
        nav.Speed = frame.GetReadOnly<StatsComponent>(entity).MoveSpeed;

      _allEntities[_allCount++] = entity;

      var isMinion = frame.Has<Minion>(entity);

      if (isMinion && frame.Has<UnitMoveTarget>(entity)) {
        EnsureCapacity(ref _minionEntities, _minionCount + 1);
        _minionEntities[_minionCount++] = entity;
      }
      else {
        if (frame.Has<UnitMoveTarget>(entity)) {
          ref readonly var target = ref frame.GetReadOnly<UnitMoveTarget>(entity);
          if (DestinationChanged(in nav, target.Target))
            NavAgentComponent.SetDestination(ref nav, target.Target);
        }
        else if (nav.HasNavDestination) {
          NavAgentComponent.Stop(ref nav);
        }

        EnsureCapacity(ref _heroEntities, _heroCount + 1);
        _heroEntities[_heroCount++] = entity;
      }
    }

    if (_allCount == 0)
      return;

    // Phase 2: Hero pathfinding via existing A* + funnel (spread across ticks)
    if (_heroCount > 0) {
      var heroSubsetCount = BuildSpreadSubset(
        _heroEntities, _heroCount, tuning.HeroSteeringSpread, frame.Tick, 0,
        ref _heroSubset);
      if (heroSubsetCount > 0)
        _navigation.AgentSystem.UpdateSteering(ref frame, _heroSubset, heroSubsetCount, frame.Tick);
    }

    // Phase 3: Minion steering via flow fields (spread across ticks)
    {
      var minionSubsetCount = BuildSpreadSubset(
        _minionEntities, _minionCount, tuning.MinionSteeringSpread, frame.Tick, 1,
        ref _minionSubset);
      if (minionSubsetCount > 0)
        UpdateMinionFlowFieldSteering(ref frame, _minionSubset, minionSubsetCount, tuning);
    }

    // Phase 4: ORCA avoidance with separate collision layers (spread across ticks)
    var avoidance = _navigation.Avoidance;
    if (avoidance != null) {
      // The runtime is constructed without a frame, so its ORCA tuning is applied here instead.
      avoidance.TimeHorizon = tuning.AvoidanceTimeHorizon;

      _minionAvoidanceGrid.Clear();
      _heroAvoidanceGrid.Clear();

      for (var i = 0; i < _allCount; i++) {
        var entity = _allEntities[i];
        ref var nav = ref frame.Get<NavAgentComponent>(entity);
        var posXZ = nav.Position.ToXZ();

        if (frame.Has<Minion>(entity))
          _minionAvoidanceGrid.Insert(entity, posXZ);
        else
          _heroAvoidanceGrid.Insert(entity, posXZ);
      }

      var avoidSubsetCount = BuildSpreadSubset(
        _allEntities, _allCount, tuning.AvoidanceSpread, frame.Tick, 2,
        ref _avoidanceSubset);

      for (var i = 0; i < avoidSubsetCount; i++) {
        var entity = _avoidanceSubset[i];
        ref var nav = ref frame.Get<NavAgentComponent>(entity);
        if (nav.Status != (byte)FPNavAgentStatus.Moving)
          continue;

        var isMinion = frame.Has<Minion>(entity);
        var grid = isMinion ? _minionAvoidanceGrid : _heroAvoidanceGrid;
        var neighborDist = isMinion ? tuning.MinionNeighborDist : avoidance.NeighborDist;
        grid.QueryRadius(nav.Position.ToXZ(), neighborDist, _nearbyAgents);
        nav.DesiredVelocity = avoidance.ComputeNewVelocity(entity, ref frame, _nearbyAgents, dt);
      }
    }

    // Phase 5: Movement integration (all agents)
    _navigation.AgentSystem.UpdateMovement(ref frame, _allEntities, _allCount, dt);

    // Phase 6: Sync back to transforms + arrival detection
    for (var i = 0; i < _allCount; i++) {
      var entity = _allEntities[i];
      ref var nav = ref frame.Get<NavAgentComponent>(entity);
      ref var transform = ref frame.Get<TransformComponent>(entity);

      transform.Position = new FPVector3(nav.Position.x, FP64.Zero, nav.Position.z);
      // nav.Velocity is an FPVector2 on the XZ plane, so .y here IS Z — this is the same
      // Atan2(x, z) yaw convention as CommandSystem and WaveSpawnSystem
      if (nav.Velocity.sqrMagnitude > FP64.Zero)
        transform.Rotation = FP64.Atan2(nav.Velocity.x, nav.Velocity.y);

      if (nav.Status == (byte)FPNavAgentStatus.Arrived)
        frame.Remove<UnitMoveTarget>(entity);
    }
  }

  private void UpdateMinionFlowFieldSteering(ref Frame frame, EntityRef[] entities, int count,
    NavigationTuningAsset tuning) {
    var query = _navigation.Query;
    var flowFields = _navigation.FlowFields;

    var arrivalDistSqr = tuning.FlowFieldArrivalDist * tuning.FlowFieldArrivalDist;
    var directSteerDistSqr = tuning.FlowFieldDirectSteerDist * tuning.FlowFieldDirectSteerDist;
    var blockedZoneSqr = tuning.BlockedZone * tuning.BlockedZone;
    var settleZoneSqr = tuning.SettleZone * tuning.SettleZone;

    for (var i = 0; i < count; i++) {
      var entity = entities[i];
      ref var nav = ref frame.Get<NavAgentComponent>(entity);
      ref readonly var moveTarget = ref frame.GetReadOnly<UnitMoveTarget>(entity);

      var goalXZ = new FPVector2(moveTarget.Target.x, moveTarget.Target.z);
      var agentXZ = nav.Position.ToXZ();
      var toTargetXZ = goalXZ - agentXZ;
      var distSqr = toTargetXZ.sqrMagnitude;

      // Arrival: reached the target, OR inside the pile and blocked (slowed by the crowd), OR
      // near the target but stuck with no progress. Settling blocked/stuck minions where they are
      // — instead of insisting on the exact shared point — is what stops the crowd shuffling and
      // lets it freeze quickly rather than compressing one minion at a time.
      var blocked = distSqr <= blockedZoneSqr && nav.CurrentSpeed <= tuning.BlockedSpeed;
      var stuck = UpdateSettleTracker(ref frame, entity, goalXZ, distSqr, tuning, settleZoneSqr);
      if (distSqr <= arrivalDistSqr || blocked || stuck) {
        nav.Status = (byte)FPNavAgentStatus.Arrived;
        nav.Velocity = FPVector2.Zero;
        nav.DesiredVelocity = FPVector2.Zero;
        if (frame.Has<MinionSettleTracker>(entity))
          frame.Remove<MinionSettleTracker>(entity);
        continue;
      }

      // Close to the slot: steer straight in, but decelerate on approach (arrival behaviour) so
      // agents ease into place instead of charging at full speed and overshooting.
      if (distSqr <= directSteerDistSqr) {
        var mag = FP64.Sqrt(distSqr);
        var speed = mag < tuning.ArrivalBrakeDist
          ? nav.Speed * mag / tuning.ArrivalBrakeDist
          : nav.Speed;
        nav.DesiredVelocity = toTargetXZ / mag * speed;
        nav.Status = (byte)FPNavAgentStatus.Moving;
        continue;
      }

      // Resolve goal triangle for flow field lookup
      var goalTri = query.FindTriangle(goalXZ);
      if (goalTri < 0) {
        nav.Status = (byte)FPNavAgentStatus.Moving;
        var mag = FP64.Sqrt(distSqr);
        nav.DesiredVelocity = toTargetXZ / mag * nav.Speed;
        continue;
      }

      // Get or create flow field for this destination
      var field = flowFields.GetOrCreate(goalTri);

      var currentTri = nav.CurrentTriangleIndex;
      if (currentTri < 0 || currentTri >= field.NextTriangle.Length) {
        nav.Status = (byte)FPNavAgentStatus.Moving;
        var mag = FP64.Sqrt(distSqr);
        nav.DesiredVelocity = toTargetXZ / mag * nav.Speed;
        continue;
      }

      var next = field.NextTriangle[currentTri];
      if (next == TriangleFlowField.AtGoal || next == TriangleFlowField.Unreachable) {
        var mag = FP64.Sqrt(distSqr);
        nav.DesiredVelocity = mag > FP64.Zero ? toTargetXZ / mag * nav.Speed : FPVector2.Zero;
      }
      else {
        var dist = FP64.Sqrt(distSqr);
        var directDir = toTargetXZ / dist;
        var exitDir = field.GetExitDirection(currentTri);
        var blended = exitDir + directDir;
        var blendMag = blended.magnitude;
        nav.DesiredVelocity = blendMag > FP64.Zero
          ? blended / blendMag * nav.Speed
          : exitDir * nav.Speed;
      }

      nav.Status = (byte)FPNavAgentStatus.Moving;
    }
  }

  // Tracks how close a minion has gotten to its slot and how long it has stalled. Returns true
  // when the minion is within the settle zone and hasn't improved for SettleStuckTicks ticks.
  private static bool UpdateSettleTracker(ref Frame frame, EntityRef entity, FPVector2 goalXZ, FP64 distSqr,
    NavigationTuningAsset tuning, FP64 settleZoneSqr) {
    var dist = FP64.Sqrt(distSqr);

    if (!frame.Has<MinionSettleTracker>(entity))
      frame.Add(entity, new MinionSettleTracker {
        TargetX = goalXZ.x,
        TargetZ = goalXZ.y,
        BestDist = dist,
        StuckTicks = 0
      });

    ref var settle = ref frame.Get<MinionSettleTracker>(entity);

    // Retargeted (new slot) → restart tracking against the new goal.
    if (settle.TargetX != goalXZ.x || settle.TargetZ != goalXZ.y) {
      settle.TargetX = goalXZ.x;
      settle.TargetZ = goalXZ.y;
      settle.BestDist = dist;
      settle.StuckTicks = 0;
      return false;
    }

    // Progress only counts if we've closed at least SettleProgressStep since the last reset, so a
    // minion crawling at a fraction of a unit per second still trips the stuck detector.
    if (dist + tuning.SettleProgressStep < settle.BestDist) {
      settle.BestDist = dist;
      settle.StuckTicks = 0;
    }
    else {
      settle.StuckTicks++;
    }

    return settle.StuckTicks >= tuning.SettleStuckTicks && distSqr <= settleZoneSqr;
  }

  private void SyncAgentPosition(ref Frame frame, EntityRef entity, ref NavAgentComponent nav,
    FPVector3 position, FP64 snapThresholdSqr) {
    var tracked = frame.Has<NavSnapTracker>(entity);

    if (tracked && nav.CurrentTriangleIndex >= 0) {
      ref readonly var snap = ref frame.GetReadOnly<NavSnapTracker>(entity);
      var deltaX = position.x - snap.LastSnappedX;
      var deltaZ = position.z - snap.LastSnappedZ;

      // Use cached value while under threshold
      if (deltaX * deltaX + deltaZ * deltaZ < snapThresholdSqr) {
        nav.Position = position;
        return;
      }
    }

    // Recalculate snap
    var snapXZ = _navigation.Query.ClosestPointOnNavMesh(position.ToXZ(), out var snapTri);
    nav.Position = snapTri >= 0
      ? new FPVector3(snapXZ.x, position.y, snapXZ.y)
      : position;

    if (snapTri >= 0)
      nav.CurrentTriangleIndex = snapTri;

    if (!tracked)
      frame.Add(entity, new NavSnapTracker());

    ref var updated = ref frame.Get<NavSnapTracker>(entity);
    updated.LastSnappedX = nav.Position.x;
    updated.LastSnappedZ = nav.Position.z;
  }

  private static bool DestinationChanged(in NavAgentComponent nav, FPVector3 target) {
    return !nav.HasNavDestination
           || nav.Destination.x != target.x
           || nav.Destination.y != target.y
           || nav.Destination.z != target.z;
  }

  private static int BuildSpreadSubset(
    EntityRef[] source, int count, int spread, int tick, int offset,
    ref EntityRef[] dest) {
    if (spread <= 1) {
      EnsureCapacity(ref dest, count);
      Array.Copy(source, dest, count);
      return count;
    }

    var bucket = ((tick - offset) % spread + spread) % spread;
    var subsetCount = 0;
    EnsureCapacity(ref dest, count);
    for (var i = 0; i < count; i++)
      if (i % spread == bucket)
        dest[subsetCount++] = source[i];
    return subsetCount;
  }

  private static void EnsureCapacity(ref EntityRef[] array, int required) {
    if (required <= array.Length)
      return;
    var newSize = array.Length;
    while (newSize < required) newSize *= 2;
    Array.Resize(ref array, newSize);
  }
}
