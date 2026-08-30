using System.Collections.Generic;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using Meesles.Avalon.Sim.Heroes;
using Meesles.Avalon.Sim.Navigation;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Deterministic.Navigation;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon;

// Advances every skill projectile and resolves collisions
//
// Everything this system remembers between calls is rebuilt from scratch inside Update. That is what
// makes it rollback-safe: a projectile's own state lives on its entity, in the frame.
public class ProjectileSystem : ISystem {
  private readonly List<EntityRef> _candidates = new();
  private readonly List<EntityRef> _expired = new();
  private readonly UnitLookup.Index _unitIndex = new();

  private SpatialHashGrid _grid; // Lazy: cell size comes off an asset, unreachable until a frame exists
  private FP64 _maxBodyRadius; // Widest body radius in the grid, so the broad phase can't miss a candidate

  public void Update(ref Frame frame) {
    if (!HasAnyProjectile(ref frame))
      return;

    var rules = frame.AssetRegistry.Get<CombatRulesAsset>();
    _grid ??= new SpatialHashGrid(rules.TargetGridCellSize);
    BuildCandidateGrid(ref frame);
    _unitIndex.Rebuild(ref frame);

    var dt = FP64.FromInt(frame.DeltaTimeMs) / FP64.FromInt(1000);
    _expired.Clear();

    var filter = frame.Filter<Projectile, TransformComponent>();
    while (filter.Next(out var entity)) {
      ref var projectile = ref frame.Get<Projectile>(entity);
      ref var transform = ref frame.Get<TransformComponent>(entity);

      var step = projectile.Speed * dt;
      if (step > projectile.RemainingDistance)
        step = projectile.RemainingDistance;

      var start = transform.Position;
      var end = start + projectile.Direction * step;

      if (TryFindHit(ref frame, in projectile, start, end, step, out var target)) {
        var source = ResolveSource(ref frame, in projectile);
        DamageApplication.ApplyDamage(ref frame, source, target, projectile.Damage, DamageType.Magical);
        ApplyOnHitEffects(ref frame, in projectile, source, target);
        SkillProjectiles.RaiseDespawned(ref frame, in projectile, end,
          UnitLookup.GetUnitId(ref frame, target), SkillProjectileEnd.Hit);
        _expired.Add(entity);
        continue;
      }

      transform.Position = end;
      projectile.RemainingDistance -= step;
      if (projectile.RemainingDistance > FP64.Zero)
        continue;

      SkillProjectiles.RaiseDespawned(ref frame, in projectile, end, 0, SkillProjectileEnd.Expired);
      _expired.Add(entity);
    }

    // Deferred: destroying inside the filter loop would pull the storage out from under it.
    foreach (var entity in _expired)
      frame.DestroyEntity(entity);
  }

  private static bool HasAnyProjectile(ref Frame frame) {
    var filter = frame.Filter<Projectile>();
    return filter.Next(out _);
  }

  // Same broad phase as TargetAcquisitionSystem: bucket every damageable unit once, then only
  // narrow-phase the handful near each bullet. Cleared and refilled every tick, which is the only
  // reason a system-owned grid survives a rollback.
  private void BuildCandidateGrid(ref Frame frame) {
    _grid.Clear();
    _maxBodyRadius = FP64.Zero;

    var filter = frame.Filter<UnitIdComponent, TeamComponent, Health, TransformComponent>();
    while (filter.Next(out var candidate)) {
      ref readonly var transform = ref frame.GetReadOnly<TransformComponent>(candidate);
      _grid.Insert(candidate, transform.Position.ToXZ());

      var bodyRadius = BodyRadius(ref frame, candidate);
      if (bodyRadius > _maxBodyRadius)
        _maxBodyRadius = bodyRadius;
    }
  }

  // Nearest thing the bullet sweeps through this tick, measured along the segment so a bullet that
  // passes two units in one step always resolves against the first one. UnitId breaks an exact tie,
  // keeping the outcome independent of grid cell order.
  private bool TryFindHit(ref Frame frame, in Projectile projectile, FPVector3 start, FPVector3 end,
    FP64 step, out EntityRef hit) {
    hit = default;

    var startXZ = start.ToXZ();
    var endXZ = end.ToXZ();
    var midpoint = (startXZ + endXZ) / FP64.FromInt(2);
    var queryRadius = step / FP64.FromInt(2) + projectile.Radius + _maxBodyRadius;
    _grid.QueryRadius(midpoint, queryRadius, _candidates);

    var found = false;
    var bestTravel = FP64.Zero;
    var bestUnitId = 0;

    for (var i = 0; i < _candidates.Count; i++) {
      var candidate = _candidates[i];
      if (!CombatTargeting.IsSkillHittable(ref frame, candidate))
        continue;

      if (!IsHostile(ref frame, in projectile, candidate))
        continue;

      var candidateXZ = frame.GetReadOnly<TransformComponent>(candidate).Position.ToXZ();
      var travel = ClosestTravelAlong(startXZ, endXZ, candidateXZ);
      var closest = startXZ + (endXZ - startXZ) * travel;

      var reach = projectile.Radius + BodyRadius(ref frame, candidate);
      if ((candidateXZ - closest).sqrMagnitude > reach * reach)
        continue;

      var unitId = frame.GetReadOnly<UnitIdComponent>(candidate).UnitId;
      if (found && (travel > bestTravel || (travel == bestTravel && unitId >= bestUnitId)))
        continue;

      found = true;
      bestTravel = travel;
      bestUnitId = unitId;
      hit = candidate;
    }

    return found;
  }

  // A projectile skill can author a slow and a burn that land where the bullet connects, both scaled
  // to the rank that fired it. Separate from the impact damage, which may be zero - Strangle is all
  // debuff. Re-read off the immutable row rather than ridden on the entity: a bullet's flight is under
  // a second, so the rank that fired it is the rank that lands it.
  private static void ApplyOnHitEffects(ref Frame frame, in Projectile projectile, EntityRef source,
    EntityRef target) {
    if (!frame.AssetRegistry.TryGet<SkillAsset>(projectile.SkillAssetId, out var skill))
      return;

    var rank = projectile.Rank;

    if (skill.BuffSpecs.Length > 0) {
      var buffTicks = TickMath.MsToTicksCeil(ref frame, skill.BuffDurationMsAtRank(rank));
      foreach (var spec in skill.BuffSpecs)
        StatBuffApplication.ApplySpec(ref frame, target, skill.AssetId, spec, rank, buffTicks);
    }

    if (skill.DotDurationMs > 0)
      DamageOverTime.Apply(ref frame, target, source, skill.AssetId,
        skill.DotDamagePerSecondAtRank(rank), TickMath.MsToTicksCeil(ref frame, skill.DotDurationMs));
  }

  // Hostility rides the team stamped at spawn: the bullet outlives its caster, and its allegiance is
  // fixed when it is fired regardless of what the caster does afterward.
  private static bool IsHostile(ref Frame frame, in Projectile projectile, EntityRef candidate) {
    return CombatTargeting.IsHostileAndAlive(ref frame, projectile.TeamId, candidate);
  }

  private EntityRef ResolveSource(ref Frame frame, in Projectile projectile) {
    return _unitIndex.TryGet(projectile.SourceUnitId, out var source) ? source : default;
  }

  // The authored body, not the nav agent's PathingRadius - a unit is pathed thinner than it is hit.
  private static FP64 BodyRadius(ref Frame frame, EntityRef entity) {
    return CombatRange.GameplayRadiusOf(ref frame, entity);
  }

  // Normalized 0..1 position along [start, end] of the point closest to `point`. A zero-length
  // segment (a bullet at the very end of its range) collapses to the start.
  private static FP64 ClosestTravelAlong(FPVector2 start, FPVector2 end, FPVector2 point) {
    var segment = end - start;
    var lengthSq = segment.sqrMagnitude;
    if (lengthSq <= FP64.Zero)
      return FP64.Zero;

    return FP64.Clamp01(FPVector2.Dot(point - start, segment) / lengthSq);
  }
}
