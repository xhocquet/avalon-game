using System.Linq;
using FluentAssertions;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Models;
using Xunit;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Tests;

public class OasisSpawnSystemTests {
  [Fact]
  public void Update_PreparesEjectsAndLandsAPickupOnSchedule() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;

    var oasisFilter = frame.Filter<Oasis, TransformComponent>();
    oasisFilter.Next(out var oasisEntity).Should().BeTrue("the map should have at least one Oasis marker");
    var oasisId = frame.GetReadOnly<Oasis>(oasisEntity).OasisId;
    var oasisPosition = frame.GetReadOnly<TransformComponent>(oasisEntity).Position;
    var pickupsBefore = CountPickups(ref frame);

    var collector = new EventCollector();
    collector.BeginTick(0);
    frame.EventRaiser = collector;

    var system = new OasisSpawnSystem();
    var totalMs = OasisSpawnSystem.SpawnIntervalMs + 2000;
    var ticks = totalMs / SimHarness.DefaultDeltaTimeMs;
    for (var i = 0; i < ticks; i++)
      system.Update(ref frame);

    var preparing = collector.Collected.OfType<OasisResourcePreparingEvent>().Should().ContainSingle().Subject;
    var ejected = collector.Collected.OfType<OasisResourceEjectedEvent>().Should().ContainSingle().Subject;
    var landed = collector.Collected.OfType<OasisResourceLandedEvent>().Should().ContainSingle().Subject;

    preparing.OasisId.Should().Be(oasisId);
    preparing.OasisPosition.Should().Be(oasisPosition);
    preparing.PickupId.Should().Be(ejected.PickupId);
    ejected.PickupId.Should().Be(landed.PickupId);
    preparing.TargetPosition.Should().Be(ejected.TargetPosition);
    ejected.TargetPosition.Should().Be(landed.Position);
    landed.Amount.Should().BeGreaterThan(0);

    CountPickups(ref frame).Should().Be(pickupsBefore + 1);
  }

  private static int CountPickups(ref Frame frame) {
    var count = 0;
    var filter = frame.Filter<Pickup>();
    while (filter.Next(out _))
      count++;
    return count;
  }
}
