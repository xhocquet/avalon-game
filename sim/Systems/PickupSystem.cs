using System.Collections.Generic;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon;

public class PickupSystem : ISystem {
  private readonly List<EntityRef> _collected = [];

  public void Update(ref Frame frame) {
    var rules = frame.AssetRegistry.Get<PickupRulesAsset>();
    if (rules == null) return;

    _collected.Clear();

    var rangeSq = rules.CollectRange * rules.CollectRange;
    var pickups = frame.Filter<Pickup, TransformComponent>();
    while (pickups.Next(out var pickupEntity)) {
      ref readonly var pickupTransform = ref frame.GetReadOnly<TransformComponent>(pickupEntity);

      var collectors = frame.Filter<Inventory, TransformComponent>();
      while (collectors.Next(out var collectorEntity)) {
        ref readonly var collectorTransform = ref frame.GetReadOnly<TransformComponent>(collectorEntity);

        var toPickup = pickupTransform.Position - collectorTransform.Position;
        toPickup.y = FP64.Zero;
        if (toPickup.sqrMagnitude > rangeSq)
          continue;

        ref readonly var pickup = ref frame.GetReadOnly<Pickup>(pickupEntity);
        ref var inventory = ref frame.Get<Inventory>(collectorEntity);
        inventory.Resources += pickup.Amount;

        _collected.Add(pickupEntity);
        break;
      }
    }

    foreach (var t in _collected)
      frame.DestroyEntity(t);
  }
}
