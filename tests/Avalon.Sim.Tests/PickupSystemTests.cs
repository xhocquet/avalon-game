using FluentAssertions;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using Meesles.Avalon.Sim.Factories;
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
    var resourcesBefore = frame.GetReadOnly<Resources>(heroEntity).Total;

    var command = SimHarness.MoveCommand(1, 0, pickup.Position.x, pickup.Position.z);
    harness.Tick(command);
    for (var tick = 0; tick < 900; tick++)
      harness.Tick();

    // The map's pickups sit close together (and an Oasis may add more nearby during this window),
    // so a hero walking toward one may collect several; check the resource gain rather than count.
    frame = harness.Frame;
    heroEntity = FindHero(ref frame, playerId: 1);
    var resourcesAfter = frame.GetReadOnly<Resources>(heroEntity).Total;

    resourcesAfter.Should().BeGreaterThan(resourcesBefore);
    ((resourcesAfter - resourcesBefore) % pickup.Amount).Should().Be(0);
  }

  [Fact]
  public void CollectedPickup_CreditsItsOwnTypeSlot() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;

    var heroEntity = FindHero(ref frame, playerId: 1);
    var heroPosition = frame.GetReadOnly<TransformComponent>(heroEntity).Position;
    SpawnPickupAt(ref frame, heroPosition, AssetIds.PickupTypeWater, amount: 7);

    harness.Tick();

    frame = harness.Frame;
    heroEntity = FindHero(ref frame, playerId: 1);
    ref readonly var resources = ref frame.GetReadOnly<Resources>(heroEntity);
    resources.CountOf(AssetIds.PickupTypeWater).Should().Be(7);
    resources.Total.Should().Be(7);
  }

  [Fact]
  public void PickupWithUnknownType_CreditsNothing() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;

    var heroEntity = FindHero(ref frame, playerId: 1);
    var heroPosition = frame.GetReadOnly<TransformComponent>(heroEntity).Position;
    SpawnPickupAt(ref frame, heroPosition, typeAssetId: 0, amount: 7);

    harness.Tick();

    frame = harness.Frame;
    heroEntity = FindHero(ref frame, playerId: 1);
    frame.GetReadOnly<Resources>(heroEntity).Total.Should().Be(0);
  }

  [Fact]
  public void MapOases_EmitAnAuthoredPickupType() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;

    var filter = frame.Filter<Oasis>();
    var found = false;
    while (filter.Next(out var entity)) {
      found = true;
      var typeAssetId = frame.GetReadOnly<Oasis>(entity).PickupTypeAssetId;
      PickupTypes.SlotOf(typeAssetId).Should().NotBe(PickupTypes.InvalidSlot);
      frame.AssetRegistry.TryGet<PickupTypeAsset>(typeAssetId, out _).Should().BeTrue();
    }

    found.Should().BeTrue();
  }

  private static void SpawnPickupAt(ref Frame frame, FPVector3 position, int typeAssetId, int amount) {
    var entity = frame.CreateEntity();
    frame.Add(entity, TransformFactory.At(position));
    frame.Add(entity, new Pickup { PickupId = 9999, Amount = amount, TypeAssetId = typeAssetId });
  }

  private static (FPVector3 Position, int Amount) FirstPickup(ref Frame frame) {
    var filter = frame.Filter<Pickup, TransformComponent>();
    filter.Next(out var entity).Should().BeTrue();
    var pickup = frame.GetReadOnly<Pickup>(entity);
    var transform = frame.GetReadOnly<TransformComponent>(entity);
    return (transform.Position, pickup.Amount);
  }

  private static EntityRef FindHero(ref Frame frame, int playerId) {
    var filter = frame.Filter<Hero, Inventory>();
    while (filter.Next(out var entity)) {
      if (frame.GetReadOnly<Hero>(entity).PlayerId == playerId)
        return entity;
    }

    Assert.Fail($"No hero found for player {playerId}");
    return default;
  }
}
