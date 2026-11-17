using System;
using System.Collections.Generic;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

// Capture on the simulation thread; render without accessing the frame. Capture and Render must not overlap.
public sealed class VisionRaster {
  private List<Source> _sources = new();
  private List<Source> _previousSources = new();
  private bool _noFog;
  private bool _captured;
  private int _teamId;
  private FPVector2 _capturedOrigin;
  private FPVector2 _capturedSize;
  public MapLayoutAsset Map { get; private set; }

  public bool Capture(ref Frame frame, int teamId, MapLayoutAsset map) { // Returns whether captured fog inputs changed.
    var noFog = CheatsController.IsFogDisabled(ref frame, teamId);
    var changed = !_captured || !ReferenceEquals(Map, map) || _teamId != teamId || _noFog != noFog ||
                  _capturedOrigin != map.VisionOrigin || _capturedSize != map.VisionSize;
    (_sources, _previousSources) = (_previousSources, _sources);
    Map = map;
    _teamId = teamId;
    _capturedOrigin = map.VisionOrigin;
    _capturedSize = map.VisionSize;
    _sources.Clear();
    _noFog = noFog;
    if (_noFog)
      return FinishCapture(changed);
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
    return FinishCapture(changed);
  }

  private bool FinishCapture(bool changed) {
    changed |= _sources.Count != _previousSources.Count;
    for (var i = 0; !changed && i < _sources.Count; i++) {
      var a = _sources[i];
      var b = _previousSources[i];
      changed = a.Position != b.Position || a.Range != b.Range || a.Radius != b.Radius || a.Walkable != b.Walkable;
    }
    _captured = true;
    return changed;
  }

  public void Render(int width, int height, byte[] pixels) {
    var map = Map;
    if (map == null || width <= 0 || height <= 0 || pixels.Length != width * height ||
        map.VisionSize.x <= FP64.Zero || map.VisionSize.y <= FP64.Zero)
      throw new ArgumentException("Invalid vision raster dimensions.");
    if (_noFog) {
      Array.Fill(pixels, (byte)255);
    }
    else {
      Array.Clear(pixels, 0, pixels.Length);
      foreach (var source in _sources)
        Visibility.RasterizeSource(map, source.Position, source.Range, source.Radius, source.Walkable,
          map.VisionOrigin, map.VisionSize, width, height, pixels);
    }
  }

  private readonly struct Source(FPVector2 position, FP64 range, FP64 radius, bool walkable) {
    public readonly FPVector2 Position = position;
    public readonly FP64 Range = range;
    public readonly FP64 Radius = radius;
    public readonly bool Walkable = walkable;
  }
}
