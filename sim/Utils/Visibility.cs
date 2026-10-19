using System;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

public static class Visibility {
  public static ulong TeamBit(int teamId) => teamId is > 0 and <= 64 ? 1UL << (teamId - 1) : 0;

  // Evaluates the supplied frame directly, including command-time changes and restored snapshots.
  public static bool IsUnitVisible(ref Frame frame, int teamId, EntityRef target) {
    if (!frame.Has<TransformComponent>(target))
      return false;
    if (frame.Has<Team>(target) && frame.GetReadOnly<Team>(target).TeamId == teamId)
      return true;
    return IsPositionVisible(ref frame, teamId, frame.GetReadOnly<TransformComponent>(target).Position.ToXZ(),
      CombatRange.GameplayRadiusOf(ref frame, target));
  }

  public static bool IsPositionVisible(ref Frame frame, int teamId, FPVector2 position,
    FP64 targetRadius = default) {
    if (CheatsController.IsFogDisabled(ref frame, teamId))
      return true;
    var rules = frame.AssetRegistry.Get<VisionRulesAsset>();
    frame.AssetRegistry.TryGet<MapLayoutAsset>(out var map);
    var sources = frame.Filter<UnitIdentity, Team, Health, TransformComponent>();
    while (sources.Next(out var source)) {
      if (frame.GetReadOnly<Team>(source).TeamId != teamId || !frame.GetReadOnly<Health>(source).IsAlive)
        continue;
      var range = rules.RangeFor(frame.GetReadOnly<UnitIdentity>(source).UnitTypeId);
      var origin = frame.GetReadOnly<TransformComponent>(source).Position.ToXZ();
      if (CanSee(map, origin, position, range, CombatRange.GameplayRadiusOf(ref frame, source), targetRadius))
        return true;
    }
    return false;
  }

  public static bool CanSee(MapLayoutAsset map, FPVector2 origin, FPVector2 position, FP64 range,
    FP64 sourceRadius, FP64 targetRadius = default, bool? sourceWalkable = null) =>
    range > FP64.Zero && (position - origin).sqrMagnitude <= range * range &&
    (map == null || map.SightGeometry.HasLineOfSight(origin, position, sourceRadius, targetRadius,
      !(sourceWalkable ?? map.SightGeometry.IsWalkable(origin))));

  // Single-channel texture, rows increasing along world +Z. No retained state or allocations.
  public static void Rasterize(ref Frame frame, int teamId, FPVector2 origin, FPVector2 size,
    int width, int height, byte[] pixels) {
    if (width <= 0 || height <= 0 || size.x <= FP64.Zero || size.y <= FP64.Zero || pixels.Length != width * height)
      throw new ArgumentException("Invalid vision raster dimensions.");
    if (CheatsController.IsFogDisabled(ref frame, teamId)) {
      Array.Fill(pixels, (byte)255);
      return;
    }
    Array.Clear(pixels, 0, pixels.Length);
    var rules = frame.AssetRegistry.Get<VisionRulesAsset>();
    frame.AssetRegistry.TryGet<MapLayoutAsset>(out var map);
    var sources = frame.Filter<UnitIdentity, Team, Health, TransformComponent>();
    while (sources.Next(out var source)) {
      if (frame.GetReadOnly<Team>(source).TeamId != teamId || !frame.GetReadOnly<Health>(source).IsAlive)
        continue;
      var position = frame.GetReadOnly<TransformComponent>(source).Position.ToXZ();
      var range = rules.RangeFor(frame.GetReadOnly<UnitIdentity>(source).UnitTypeId);
      var radius = CombatRange.GameplayRadiusOf(ref frame, source);
      var sourceWalkable = map == null || map.SightGeometry.IsWalkable(position);
      RasterizeSource(map, position, range, radius, sourceWalkable, origin, size, width, height, pixels);
    }
  }

  internal static void RasterizeSource(MapLayoutAsset map, FPVector2 position, FP64 range, FP64 radius,
    bool sourceWalkable, FPVector2 origin, FPVector2 size, int width, int height, byte[] pixels) {
    var dx = size.x / FP64.FromInt(width);
    var dz = size.y / FP64.FromInt(height);
    var half = FP64.One / FP64.FromInt(2);
    var minX = FP64.Floor((position.x - range - origin.x) / dx).ToInt();
    var maxX = FP64.Floor((position.x + range - origin.x) / dx).ToInt();
    var minZ = FP64.Floor((position.y - range - origin.y) / dz).ToInt();
    var maxZ = FP64.Floor((position.y + range - origin.y) / dz).ToInt();
    minX = minX < 0 ? 0 : minX;
    minZ = minZ < 0 ? 0 : minZ;
    maxX = maxX >= width ? width - 1 : maxX;
    maxZ = maxZ >= height ? height - 1 : maxZ;
    for (var z = minZ; z <= maxZ; z++)
      for (var x = minX; x <= maxX; x++) {
        var index = z * width + x;
        if (pixels[index] != 0)
          continue;
        var point = origin + new FPVector2((FP64.FromInt(x) + half) * dx, (FP64.FromInt(z) + half) * dz);
        if (CanSee(map, position, point, range, radius, sourceWalkable: sourceWalkable))
          pixels[index] = 255;
      }
  }
}
