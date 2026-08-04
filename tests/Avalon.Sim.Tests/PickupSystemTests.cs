using FluentAssertions;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using Xunit;

namespace Meesles.Avalon.Sim.Tests;

public class PickupSystemTests {
  [Fact]
  public void WalkingHeroIntoPickup_GrantsResourcesAndRemovesPickup() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;

    var pickup = FirstPickup(ref frame);
    var heroEntity = FindHero(ref frame, playerId: 1);
    var resourcesBefore = frame.GetReadOnly<InventoryComponent>(heroEntity).Resources;

    var command = SimHarness.MoveCommand(1, 0, pickup.Position.x, pickup.Position.z);
    harness.Tick(command);
    for (var tick = 0; tick < 900; tick++)
      harness.Tick();

    // The map's pickups sit close together (and an Oasis may add more nearby during this window),
    // so a hero walking toward one may collect several; check the resource gain rather than count.
    frame = harness.Frame;
    heroEntity = FindHero(ref frame, playerId: 1);
    var resourcesAfter = frame.GetReadOnly<InventoryComponent>(heroEntity).Resources;

    resourcesAfter.Should().BeGreaterThan(resourcesBefore);
    ((resourcesAfter - resourcesBefore) % pickup.Amount).Should().Be(0);
  }

  private static (FPVector3 Position, int Amount) FirstPickup(ref Frame frame) {
    var filter = frame.Filter<Pickup, TransformComponent>();
    filter.Next(out var entity).Should().BeTrue();
    var pickup = frame.GetReadOnly<Pickup>(entity);
    var transform = frame.GetReadOnly<TransformComponent>(entity);
    return (transform.Position, pickup.Amount);
  }

  private static EntityRef FindHero(ref Frame frame, int playerId) {
    var filter = frame.Filter<Player, InventoryComponent>();
    while (filter.Next(out var entity)) {
      if (frame.GetReadOnly<Player>(entity).PlayerId == playerId)
        return entity;
    }

    Assert.Fail($"No hero found for player {playerId}");
    return default;
  }
}
