using FluentAssertions;
using Meesles.Avalon.Sim.Models;
using Xunit;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Tests;

public class DeathSystemTests {
  [Fact]
  public void Update_RemovesDeadUnitAndRaisesUnitDiedEvent() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    EntityRef entity = GetFirstBaseEntity(ref frame);
    int unitId = frame.GetReadOnly<Unit>(entity).UnitId;
    int unitTypeId = frame.GetReadOnly<Unit>(entity).UnitTypeId;
    var deathPosition = new FPVector3(FP64.FromInt(3), FP64.Zero, FP64.FromInt(4));
    frame.Get<TransformComponent>(entity).Position = deathPosition;
    frame.Get<Health>(entity).Current = 0;

    var collector = new EventCollector();
    collector.BeginTick(7);
    frame.EventRaiser = collector;

    var system = new DeathSystem();
    system.Update(ref frame);

    UnitLookup.TryGetEntityByUnitId(ref frame, unitId, out _).Should().BeFalse();
    collector.Count.Should().Be(1);
    var evt = collector.Collected[0].Should().BeOfType<UnitDiedEvent>().Subject;
    evt.Tick.Should().Be(7);
    evt.UnitId.Should().Be(unitId);
    evt.UnitTypeId.Should().Be(unitTypeId);
    evt.Position.Should().Be(deathPosition);
  }

  [Fact]
  public void Update_LeavesLivingUnitsAlone() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    EntityRef entity = GetFirstBaseEntity(ref frame);
    int unitId = frame.GetReadOnly<Unit>(entity).UnitId;
    frame.Get<Health>(entity).Current = 1;

    var collector = new EventCollector();
    collector.BeginTick(7);
    frame.EventRaiser = collector;

    var system = new DeathSystem();
    system.Update(ref frame);

    UnitLookup.TryGetEntityByUnitId(ref frame, unitId, out var resolved).Should().BeTrue();
    resolved.Should().Be(entity);
    collector.Count.Should().Be(0);
  }

  [Fact]
  public void Update_DoesNotDestroyDeadPlayerUnits() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    EntityRef entity = GetFirstPlayerEntity(ref frame);
    int unitId = frame.GetReadOnly<Unit>(entity).UnitId;
    frame.Get<Health>(entity).Current = 0;

    var system = new DeathSystem();
    system.Update(ref frame);

    UnitLookup.TryGetEntityByUnitId(ref frame, unitId, out var resolved).Should().BeTrue();
    resolved.Should().Be(entity);
  }

  private static EntityRef GetFirstBaseEntity(ref Frame frame) {
    var filter = frame.Filter<Base, Unit, Health, TransformComponent>();
    if (filter.Next(out var entity))
      return entity;

    throw new Xunit.Sdk.XunitException("Expected an initialized base entity.");
  }

  private static EntityRef GetFirstPlayerEntity(ref Frame frame) {
    var filter = frame.Filter<Player, Unit, Health, TransformComponent>();
    if (filter.Next(out var entity))
      return entity;

    throw new Xunit.Sdk.XunitException("Expected an initialized player entity.");
  }
}
