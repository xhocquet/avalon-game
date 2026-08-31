using System.Collections.Generic;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using Meesles.Avalon.Sim.Factories;
using Meesles.Avalon.Sim.Heroes;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon;

// Runs the trail lifecycle: emitters drop one segment per interval at the caster's current position,
// segments linger and slow (or buff) whatever their width catches, then expire. Registered beside
// ProjectileSystem because it is the same shape - entities that carry their own state in the frame,
// a system that rebuilds everything it needs each tick and so survives a rollback with no snapshot of
// its own.
//
// The emitter's drop clock could live in TimedEffectSystem with the other countdowns, but a drop
// spawns an entity and the contact test walks the unit list, so the whole cycle sits here with the
// segment mechanics rather than split across two systems.
public class TrailSystem : ISystem {
  private readonly List<EntityRef> _dropping = new();
  private readonly List<EntityRef> _finished = new();
  private readonly List<EntityRef> _expired = new();
  private readonly List<EntityRef> _units = new();

  public void Update(ref Frame frame) {
    var hasEmitter = HasAny<TrailEmitter>(ref frame);
    var hasSegment = HasAny<TrailSegment>(ref frame);
    if (!hasEmitter && !hasSegment)
      return;

    if (hasEmitter)
      AdvanceEmitters(ref frame);

    // A segment born this tick starts contact-testing next tick, the way a projectile does not
    // collide on its spawn tick.
    if (hasSegment)
      AdvanceSegments(ref frame);
  }

  // Collect first, mutate after: a drop creates a segment entity and reaping an exhausted emitter
  // removes this filter's own component type.
  private void AdvanceEmitters(ref Frame frame) {
    _dropping.Clear();
    _finished.Clear();

    var filter = frame.Filter<TrailEmitter, TransformComponent>();
    while (filter.Next(out var entity)) {
      ref readonly var emitter = ref frame.GetReadOnly<TrailEmitter>(entity);
      if (!emitter.IsEmitting)
        _finished.Add(entity);
      else if (emitter.IsDropDue(frame.Tick))
        _dropping.Add(entity);
    }

    foreach (var entity in _dropping) {
      DropSegment(ref frame, entity);

      ref var emitter = ref frame.Get<TrailEmitter>(entity);
      emitter.SegmentsRemaining--;
      emitter.NextDropTick += emitter.IntervalTicks;
      if (emitter.SegmentsRemaining <= 0) {
        emitter.Clear();
        _finished.Add(entity);
      }
    }

    foreach (var entity in _finished)
      frame.Remove<TrailEmitter>(entity);
  }

  private static void DropSegment(ref Frame frame, EntityRef caster) {
    ref readonly var emitter = ref frame.GetReadOnly<TrailEmitter>(caster);
    var position = frame.GetReadOnly<TransformComponent>(caster).Position;
    var teamId = frame.Has<Team>(caster) ? frame.GetReadOnly<Team>(caster).TeamId : 0;

    var segment = new TrailSegment {
      SegmentId = IdCounter<TrailSegmentIdCounter>.Next(ref frame),
      SourceUnitId = UnitLookup.GetUnitId(ref frame, caster),
      TeamId = teamId,
      SkillAssetId = emitter.SkillAssetId,
      Rank = emitter.Rank,
      ExpiryTick = frame.Tick + emitter.SegmentLifetimeTicks,
      Width = emitter.Width
    };

    var entity = frame.CreateEntity();
    frame.Add(entity, TransformFactory.At(position));
    frame.Add(entity, segment);

    RaiseSegmentSpawned(ref frame, in segment, position, emitter.SegmentLifetimeTicks);
  }

  // One pass over the units feeds every segment's contact test, the same reason ProjectileSystem
  // buckets units once per tick.
  private void AdvanceSegments(ref Frame frame) {
    _units.Clear();
    var units = frame.Filter<UnitIdentity, Team, Health, TransformComponent>();
    while (units.Next(out var unit))
      if (CombatTargeting.IsSkillHittable(ref frame, unit))
        _units.Add(unit);

    _expired.Clear();
    var segments = frame.Filter<TrailSegment, TransformComponent>();
    while (segments.Next(out var entity)) {
      if (frame.Tick >= frame.GetReadOnly<TrailSegment>(entity).ExpiryTick) {
        _expired.Add(entity);
        continue;
      }

      ApplyContact(ref frame, entity);
    }

    foreach (var entity in _expired)
      frame.DestroyEntity(entity);
  }

  // Refreshes the row's buff block on every hostile whose body overlaps the circle. Keyed on the row,
  // so a unit standing in the trail keeps the effect topped up and one that walks out loses it a
  // buff-duration later.
  private void ApplyContact(ref Frame frame, EntityRef segmentEntity) {
    ref readonly var segment = ref frame.GetReadOnly<TrailSegment>(segmentEntity);
    if (!frame.AssetRegistry.TryGet<SkillAsset>(segment.SkillAssetId, out var skill))
      return;
    if (skill.BuffSpecs.Length == 0)
      return;

    var buffTicks = TickMath.MsToTicksCeil(ref frame, skill.BuffDurationMsAtRank(segment.Rank));
    if (buffTicks <= 0)
      return;

    var center = frame.GetReadOnly<TransformComponent>(segmentEntity).Position.ToXZ();

    for (var i = 0; i < _units.Count; i++) {
      var unit = _units[i];
      if (!CombatTargeting.IsHostileAndAlive(ref frame, segment.TeamId, unit))
        continue;

      var offset = frame.GetReadOnly<TransformComponent>(unit).Position.ToXZ() - center;
      var reach = segment.Width + CombatRange.GameplayRadiusOf(ref frame, unit);
      if (offset.sqrMagnitude > reach * reach)
        continue;

      foreach (var spec in skill.BuffSpecs)
        StatBuffApplication.ApplySpec(ref frame, unit, skill.AssetId, spec, segment.Rank, buffTicks);
    }
  }

  private static bool HasAny<T>(ref Frame frame) where T : unmanaged, IComponent {
    var filter = frame.Filter<T>();
    return filter.Next(out _);
  }

  private static void RaiseSegmentSpawned(ref Frame frame, in TrailSegment segment, FPVector3 position,
    int lifetimeTicks) {
    if (frame.EventRaiser == null)
      return;

    var evt = EventPool.Get<SkillTrailSegmentSpawnedEvent>();
    evt.SegmentId = segment.SegmentId;
    evt.SourceUnitId = segment.SourceUnitId;
    evt.SkillAssetId = segment.SkillAssetId;
    evt.Position = position;
    evt.Width = segment.Width;
    evt.LifetimeTicks = lifetimeTicks;
    frame.EventRaiser.RaiseEvent(evt);
  }
}
