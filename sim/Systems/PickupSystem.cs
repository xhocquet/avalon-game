using System.Collections.Generic;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon;

public class PickupSystem : ISystem {
  private readonly List<EntityRef> _collected = [];
  private readonly List<Collector> _collectors = [];

  public void Update(ref Frame frame) {
    var rules = frame.AssetRegistry.Get<PickupRulesAsset>();

    _collected.Clear();
    CollectCollectors(ref frame);
    if (_collectors.Count == 0)
      return;

    var rangeSq = rules.CollectRange * rules.CollectRange;
    var pickups = frame.Filter<Pickup, TransformComponent>();
    while (pickups.Next(out var pickupEntity)) {
      ref readonly var pickupTransform = ref frame.GetReadOnly<TransformComponent>(pickupEntity);

      for (var i = 0; i < _collectors.Count; i++) {
        var collector = _collectors[i];

        var toPickup = pickupTransform.Position - collector.Position;
        toPickup.y = FP64.Zero;
        if (toPickup.sqrMagnitude > rangeSq)
          continue;

        ref readonly var pickup = ref frame.GetReadOnly<Pickup>(pickupEntity);
        ref var inventory = ref frame.Get<Inventory>(collector.Entity);
        inventory.Resources += pickup.Amount;

        _collected.Add(pickupEntity);
        break;
      }
    }

    foreach (var t in _collected)
      frame.DestroyEntity(t);
  }

  // Collectors are heroes — one per player — so the set is small and fixed for the tick.
  // Snapshotting it once keeps the pickup loop off the filter/storage machinery: nothing
  // here moves a transform, so the cached positions stay valid for the whole pass.
  private void CollectCollectors(ref Frame frame) {
    _collectors.Clear();

    var filter = frame.Filter<Inventory, TransformComponent>();
    while (filter.Next(out var entity))
      _collectors.Add(new Collector(entity, frame.GetReadOnly<TransformComponent>(entity).Position));
  }

  private readonly struct Collector(EntityRef entity, FPVector3 position) {
    public readonly EntityRef Entity = entity;
    public readonly FPVector3 Position = position;
  }
}
