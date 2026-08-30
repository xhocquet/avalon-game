using Meesles.Avalon.Sim.Components;
using Meesles.Avalon.Sim.Factories;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Heroes;

// Shared spawn side of the projectile lifecycle: skills put bullets in the air through here,
// ProjectileSystem advances and resolves them. Kept at the root of Heroes/ with the rest of the
// plumbing, so any hero's skill set can fire a volley without owning the entity assembly.
public static class SkillProjectiles {
  // Fires `count` projectiles travelling in the same direction, spread laterally across the
  // caster's facing rather than fanned outward - the three parallel bars Crystal Bullets telegraphs.
  // Offsets are symmetric about the aim line, so an odd count always puts one bullet dead centre.
  public static void SpawnVolley(ref Frame frame, in SkillCastContext ctx, FPVector3 direction,
    int count, FP64 spacing, FP64 speed, FP64 range, FP64 radius, FP64 spawnOffset, FP64 damage) {
    if (count <= 0 || speed <= FP64.Zero || range <= FP64.Zero)
      return;

    // Perpendicular in the XZ plane, matching the Atan2(x, z) yaw convention: for direction +Z this
    // is +X, so index 0 starts on the caster's left and the volley reads left to right.
    var right = new FPVector3(direction.z, FP64.Zero, -direction.x);
    var firstOffset = -spacing * FP64.FromInt(count - 1) / FP64.FromInt(2);
    var muzzle = ctx.CasterPosition + direction * spawnOffset;
    var teamId = frame.Has<TeamComponent>(ctx.Caster)
      ? frame.GetReadOnly<TeamComponent>(ctx.Caster).TeamId
      : 0;
    var sourceUnitId = UnitLookup.GetUnitId(ref frame, ctx.Caster);
    var yaw = FP64.Atan2(direction.x, direction.z);

    for (var i = 0; i < count; i++) {
      var origin = muzzle + right * (firstOffset + spacing * FP64.FromInt(i));
      var projectile = new Projectile {
        ProjectileId = IdCounter<ProjectileIdCounter>.Next(ref frame),
        SourceUnitId = sourceUnitId,
        TeamId = teamId,
        Damage = damage,
        SkillAssetId = frame.GetReadOnly<SkillsComponent>(ctx.Caster).GetSkillAssetId(ctx.Slot),
        Slot = ctx.Slot,
        Rank = ctx.Rank,
        Index = i,
        Direction = direction,
        Speed = speed,
        RemainingDistance = range,
        Radius = radius
      };

      var entity = frame.CreateEntity();
      frame.Add(entity, TransformFactory.At(origin, yaw));
      frame.Add(entity, projectile);

      RaiseSpawned(ref frame, in projectile, origin, range);
    }
  }

  private static void RaiseSpawned(ref Frame frame, in Projectile projectile, FPVector3 origin, FP64 range) {
    if (frame.EventRaiser == null)
      return;

    var evt = EventPool.Get<SkillProjectileSpawnedEvent>();
    evt.ProjectileId = projectile.ProjectileId;
    evt.SourceUnitId = projectile.SourceUnitId;
    evt.SkillAssetId = projectile.SkillAssetId;
    evt.Slot = projectile.Slot;
    evt.Index = projectile.Index;
    evt.Origin = origin;
    evt.Direction = projectile.Direction;
    evt.Speed = projectile.Speed;
    evt.Range = range;
    evt.Radius = projectile.Radius;
    frame.EventRaiser.RaiseEvent(evt);
  }

  // Closes out one spawned projectile. ProjectileSystem calls this for both endings so every
  // ProjectileId the view saw born is one it also sees die.
  public static void RaiseDespawned(ref Frame frame, in Projectile projectile, FPVector3 position,
    int hitUnitId, SkillProjectileEnd reason) {
    if (frame.EventRaiser == null)
      return;

    var evt = EventPool.Get<SkillProjectileDespawnedEvent>();
    evt.ProjectileId = projectile.ProjectileId;
    evt.Position = position;
    evt.HitUnitId = hitUnitId;
    evt.Reason = (int)reason;
    frame.EventRaiser.RaiseEvent(evt);
  }
}
