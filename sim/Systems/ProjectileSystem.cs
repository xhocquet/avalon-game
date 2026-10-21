using System.Collections.Generic;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using Meesles.Avalon.Sim.Navigation;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

// Projectile state lives in frame components; per-tick caches rebuild in Update.
public class ProjectileSystem(UnitSpatialIndex spatial = null) : ISystem {
  private readonly List<EntityRef> _candidates = new();
  private readonly List<EntityRef> _expired = new();
  private readonly UnitLookup.Index _unitIndex = new();

  private readonly UnitSpatialIndex _standaloneIndex = spatial == null ? new UnitSpatialIndex() : null;
  private FP64 _maxBodyRadius; // Keeps the broad phase conservative.

  public void Update(ref Frame frame) {
    if (!HasAnyProjectile(ref frame))
      return;

    _standaloneIndex?.Update(ref frame);
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
        DamageController.ApplyDamage(ref frame, source, target, projectile.Damage, DamageType.Magical);
        ApplyOnHitEffects(ref frame, in projectile, source, target);
        ProjectileController.RaiseDespawned(ref frame, in projectile, end,
          UnitLookup.GetUnitId(ref frame, target), SkillProjectileEnd.Hit);
        _expired.Add(entity);
        continue;
      }

      transform.Position = end;
      projectile.RemainingDistance -= step;
      if (projectile.RemainingDistance > FP64.Zero)
        continue;

      ProjectileController.RaiseDespawned(ref frame, in projectile, end, 0, SkillProjectileEnd.Expired);
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

  // Rebuilt every tick so the system-owned grid stays rollback-safe.
  private void BuildCandidateGrid(ref Frame frame) {
    _maxBodyRadius = FP64.Zero;

    var filter = frame.Filter<UnitIdentity, Team, Health, TransformComponent>();
    while (filter.Next(out var candidate)) {
      var bodyRadius = BodyRadius(ref frame, candidate);
      if (bodyRadius > _maxBodyRadius)
        _maxBodyRadius = bodyRadius;
    }
  }

  // Hit the first unit along the segment; UnitId breaks equal-distance ties.
  private bool TryFindHit(ref Frame frame, in Projectile projectile, FPVector3 start, FPVector3 end,
    FP64 step, out EntityRef hit) {
    hit = default;

    var startXZ = start.ToXZ();
    var endXZ = end.ToXZ();
    var midpoint = (startXZ + endXZ) / FP64.FromInt(2);
    var queryRadius = step / FP64.FromInt(2) + projectile.Radius + _maxBodyRadius;
    (spatial ?? _standaloneIndex).Grid.QueryRadius(midpoint, queryRadius, _candidates);

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
      var travel = Planar.ClosestPointTravel(startXZ, endXZ, candidateXZ);
      var closest = startXZ + (endXZ - startXZ) * travel;

      var reach = projectile.Radius + BodyRadius(ref frame, candidate);
      if (Planar.DistanceSq(candidateXZ, closest) > reach * reach)
        continue;

      var unitId = frame.GetReadOnly<UnitIdentity>(candidate).UnitId;
      if (found && (travel > bestTravel || (travel == bestTravel && unitId >= bestUnitId)))
        continue;

      found = true;
      bestTravel = travel;
      bestUnitId = unitId;
      hit = candidate;
    }

    return found;
  }

  // Apply rank-scaled authored on-hit effects.
  private static void ApplyOnHitEffects(ref Frame frame, in Projectile projectile, EntityRef source,
    EntityRef target) {
    if (!frame.AssetRegistry.TryGet<SkillAsset>(projectile.SkillAssetId, out var skill))
      return;

    var rank = projectile.Rank;

    if (skill.BuffSpecs.Length > 0)
      BuffsController.ApplySkill(ref frame, target, skill, rank, projectile.SourceUnitId);

    if (skill.DotDurationMs > 0)
      DamageOverTimeController.ApplySkill(ref frame, target, projectile.SourceUnitId, skill, rank);
  }

  // Projectile allegiance is fixed at spawn.
  private static bool IsHostile(ref Frame frame, in Projectile projectile, EntityRef candidate) {
    return CombatTargeting.IsHostileAndAlive(ref frame, projectile.TeamId, candidate);
  }

  private EntityRef ResolveSource(ref Frame frame, in Projectile projectile) {
    return _unitIndex.TryGet(projectile.SourceUnitId, out var source) ? source : default;
  }

  // Combat body radius differs from pathing radius.
  private static FP64 BodyRadius(ref Frame frame, EntityRef entity) {
    return CombatRange.GameplayRadiusOf(ref frame, entity);
  }

}
