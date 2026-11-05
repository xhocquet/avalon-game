using System.Collections.Generic;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using Meesles.Avalon.Sim.Navigation;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

public sealed class VisionSystem(UnitSpatialIndex spatial = null) : ISystem {
  private readonly List<EntityRef> _candidates = new();

  public void Update(ref Frame frame) {
    var rules = frame.AssetRegistry.Get<VisionRulesAsset>();
    frame.AssetRegistry.TryGet<MapLayoutAsset>(out var map);
    ulong revealedTeams = 0;
    var heroes = frame.Filter<Hero, Team>();
    while (heroes.Next(out var hero))
      if (CheatsController.IsEnabled(ref frame, frame.GetReadOnly<Hero>(hero).PlayerId, CheatFlags.NoFog))
        revealedTeams |= Visibility.TeamBit(frame.GetReadOnly<Team>(hero).TeamId);
    var targets = frame.Filter<UnitIdentity, Team, Health, TransformComponent>();
    while (targets.Next(out var target)) {
      if (!frame.Has<UnitVisibility>(target))
        frame.Add(target, new UnitVisibility());
      frame.Get<UnitVisibility>(target).Teams = revealedTeams | Visibility.TeamBit(frame.GetReadOnly<Team>(target).TeamId);
    }

    var sources = frame.Filter<UnitIdentity, Team, Health, TransformComponent>();
    while (sources.Next(out var source)) {
      if (!frame.GetReadOnly<Health>(source).IsAlive)
        continue;
      var origin = frame.GetReadOnly<TransformComponent>(source).Position.ToXZ();
      var range = rules.RangeFor(frame.GetReadOnly<UnitIdentity>(source).UnitTypeId);
      var bit = Visibility.TeamBit(frame.GetReadOnly<Team>(source).TeamId);
      var radius = CombatRange.GameplayRadiusOf(ref frame, source);
      Reveal(ref frame, map, origin, range, radius, bit);
    }
    if (map != null)
      for (var i = 0; i < map.MarkerCount; i++)
        Reveal(ref frame, map, map.MarkerPositions[i].ToXZ(), rules.RangeForMarker(map.MarkerTypes[i]),
          FP64.Zero, Visibility.TeamBit(map.MarkerTeams[i]));
  }

  private void Reveal(ref Frame frame, MapLayoutAsset map, FPVector2 origin, FP64 range, FP64 radius, ulong bit) {
    if (range <= FP64.Zero || bit == 0)
      return;
    var sourceWalkable = map == null || map.SightGeometry.IsWalkable(origin);
    _candidates.Clear();
    if (spatial != null)
      spatial.Grid.QueryRadius(origin, range, _candidates);
    else {
      var all = frame.Filter<UnitIdentity, Team, Health, TransformComponent>();
      while (all.Next(out var target))
        _candidates.Add(target);
    }
    foreach (var target in _candidates) {
      ref var visibility = ref frame.Get<UnitVisibility>(target);
      if ((visibility.Teams & bit) != 0)
        continue;
      var position = frame.GetReadOnly<TransformComponent>(target).Position.ToXZ();
      if (Visibility.CanSee(map, origin, position, range, radius, CombatRange.GameplayRadiusOf(ref frame, target),
            sourceWalkable))
        visibility.Teams |= bit;
    }
  }
}
