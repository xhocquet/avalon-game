using System.Collections.Generic;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon;

// Walk-over auto-collect: any Inventory-bearing entity (heroes today) that comes within
// PickupRange of a Pickup adds its Amount to Inventory.Resources and the pickup is destroyed.
// No explicit command is needed to collect — players just walk there via the existing MoveCommand.
public class PickupSystem : ISystem {
  private static readonly FP64 PickupRange = FP64.FromDouble(1.5);
  private readonly List<EntityRef> _collected = new();

  public void Update(ref Frame frame) {
    _collected.Clear();

    var rangeSq = PickupRange * PickupRange;
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

    for (var i = 0; i < _collected.Count; i++)
      frame.DestroyEntity(_collected[i]);
  }
}
