using System;
using System.Collections.Generic;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

// Capture on the simulation thread; render without accessing the frame. Capture and Render must not overlap.
public sealed class VisionRaster {
  private readonly List<Source> _sources = new();
  private bool _noFog;
  public MapLayoutAsset Map { get; private set; }

  public void Capture(ref Frame frame, int teamId, MapLayoutAsset map) {
    Map = map;
    _sources.Clear();
    _noFog = CheatsController.IsFogDisabled(ref frame, teamId);
    if (_noFog)
      return;
    var rules = frame.AssetRegistry.Get<VisionRulesAsset>();
    var sources = frame.Filter<UnitIdentity, Team, Health, TransformComponent>();
    while (sources.Next(out var entity)) {
      if (frame.GetReadOnly<Team>(entity).TeamId != teamId || !frame.GetReadOnly<Health>(entity).IsAlive)
        continue;
      var position = frame.GetReadOnly<TransformComponent>(entity).Position.ToXZ();
      _sources.Add(new Source(position, rules.RangeFor(frame.GetReadOnly<UnitIdentity>(entity).UnitTypeId),
        CombatRange.GameplayRadiusOf(ref frame, entity), map.SightGeometry.IsWalkable(position)));
    }
    for (var i = 0; i < map.MarkerCount; i++) {
      var range = rules.RangeForMarker(map.MarkerTypes[i]);
      if (map.MarkerTeams[i] != teamId || range <= FP64.Zero)
        continue;
      var position = map.MarkerPositions[i].ToXZ();
      _sources.Add(new Source(position, range, FP64.Zero, map.SightGeometry.IsWalkable(position)));
    }
  }

  public void Render(int width, int height, byte[] pixels) {
    var map = Map;
    if (map == null || width <= 0 || height <= 0 || pixels.Length != width * height ||
        map.VisionSize.x <= FP64.Zero || map.VisionSize.y <= FP64.Zero)
      throw new ArgumentException("Invalid vision raster dimensions.");
    if (_noFog) {
      Array.Fill(pixels, (byte)255);
      return;
    }
    Array.Clear(pixels, 0, pixels.Length);
    foreach (var source in _sources)
      Visibility.RasterizeSource(map, source.Position, source.Range, source.Radius, source.Walkable,
        map.VisionOrigin, map.VisionSize, width, height, pixels);
  }

  private readonly struct Source(FPVector2 position, FP64 range, FP64 radius, bool walkable) {
    public readonly FPVector2 Position = position;
    public readonly FP64 Range = range;
    public readonly FP64 Radius = radius;
    public readonly bool Walkable = walkable;
  }
}
