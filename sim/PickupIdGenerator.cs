using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

// Single global sequence for Pickup.PickupId, shared by both statically map-authored pickups
// (SimulationSetup.SpawnPickups) and ones an Oasis spawns at runtime, so ids never collide.
public static class PickupIdGenerator {
  public const int FirstPickupId = 1;

  public static void Initialize(ref Frame frame, int nextPickupId = FirstPickupId) {
    if (frame.TryGetSingleton<PickupIdCounter>(out _)) return;

    var entity = frame.CreateEntity();
    frame.Add(entity, new PickupIdCounter { NextPickupId = nextPickupId });
  }

  public static int Next(ref Frame frame) {
    Initialize(ref frame);

    ref var state = ref frame.GetSingleton<PickupIdCounter>();
    var pickupId = state.NextPickupId;
    state.NextPickupId += 1;
    return pickupId;
  }
}
