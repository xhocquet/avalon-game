using FluentAssertions;
using Meesles.Avalon.Sim.Components;
using Meesles.Avalon.Sim.Factories;
using Xunit;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Tests;

public class DeathSystemTests {
  [Fact]
  public void Update_RemovesDeadCrystalAndRaisesCrystalDestroyedEvent() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    EntityRef crystal = GetFirstCrystalEntity(ref frame);
    EntityRef attacker = GetEnemyCombatUnit(ref frame, crystal);
    int unitId = frame.GetReadOnly<Unit>(crystal).UnitId;
    int crystalId = frame.GetReadOnly<Crystal>(crystal).CrystalId;
    int teamId = frame.GetReadOnly<Team>(crystal).TeamId;
    int ownerId = frame.GetReadOnly<OwnerComponent>(crystal).OwnerId;
    int destroyerUnitId = frame.GetReadOnly<Unit>(attacker).UnitId;
    int destroyerTeamId = frame.GetReadOnly<Team>(attacker).TeamId;
    int destroyerOwnerId = frame.GetReadOnly<OwnerComponent>(attacker).OwnerId;
    frame.Get<Combat>(attacker).Target = crystal;
    frame.Get<Health>(crystal).Current = 0;

    var collector = new EventCollector();
    collector.BeginTick(7);
    frame.EventRaiser = collector;

    var system = new DeathSystem();
    system.Update(ref frame);

    UnitLookup.TryGetEntityByUnitId(ref frame, unitId, out _).Should().BeFalse();
    collector.Count.Should().Be(1);
    var evt = collector.Collected[0].Should().BeOfType<CrystalDestroyedEvent>().Subject;
    evt.Tick.Should().Be(7);
    evt.UnitId.Should().Be(unitId);
    evt.CrystalId.Should().Be(crystalId);
    evt.TeamId.Should().Be(teamId);
    evt.OwnerId.Should().Be(ownerId);
    evt.DestroyerUnitId.Should().Be(destroyerUnitId);
    evt.DestroyerTeamId.Should().Be(destroyerTeamId);
    evt.DestroyerOwnerId.Should().Be(destroyerOwnerId);
  }

  [Fact]
  public void Update_RemovesDeadTurretAndRaisesTurretDestroyedEvent() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    EntityRef turret = GetFirstTurretEntity(ref frame);
    EntityRef attacker = GetEnemyCombatUnit(ref frame, turret);
    int unitId = frame.GetReadOnly<Unit>(turret).UnitId;
    int destroyerUnitId = frame.GetReadOnly<Unit>(attacker).UnitId;
    frame.Get<Combat>(attacker).Target = turret;
    frame.Get<Health>(turret).Current = 0;

    var collector = new EventCollector();
    collector.BeginTick(7);
    frame.EventRaiser = collector;

    var system = new DeathSystem();
    system.Update(ref frame);

    UnitLookup.TryGetEntityByUnitId(ref frame, unitId, out _).Should().BeFalse();
    collector.Count.Should().Be(1);
    var evt = collector.Collected[0].Should().BeOfType<TurretDestroyedEvent>().Subject;
    evt.Tick.Should().Be(7);
    evt.UnitId.Should().Be(unitId);
    evt.DestroyerUnitId.Should().Be(destroyerUnitId);
  }

  [Fact]
  public void Update_RemovesDeadNonStructureUnitAndRaisesUnitDiedEvent() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    EntityRef entity = SpawnTestMinion(ref frame);
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
    EntityRef entity = GetFirstCrystalEntity(ref frame);
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
  public void Update_DoesNotDestroyDeadHeroes() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    EntityRef entity = GetFirstHeroEntity(ref frame);
    int unitId = frame.GetReadOnly<Unit>(entity).UnitId;
    frame.Get<Health>(entity).Current = 0;

    var system = new DeathSystem();
    system.Update(ref frame);

    UnitLookup.TryGetEntityByUnitId(ref frame, unitId, out var resolved).Should().BeTrue();
    resolved.Should().Be(entity);
  }

  private static EntityRef GetFirstCrystalEntity(ref Frame frame) {
    var filter = frame.Filter<Crystal, Unit, OwnerComponent, Health, TransformComponent>();
    if (filter.Next(out var entity))
      return entity;

    throw new Xunit.Sdk.XunitException("Expected an initialized crystal entity.");
  }

  private static EntityRef GetFirstTurretEntity(ref Frame frame) {
    var filter = frame.Filter<Turret, Unit, Health, TransformComponent>();
    if (filter.Next(out var entity))
      return entity;

    throw new Xunit.Sdk.XunitException("Expected an initialized turret entity.");
  }

  private static EntityRef GetEnemyCombatUnit(ref Frame frame, EntityRef target) {
    int targetTeamId = frame.GetReadOnly<Team>(target).TeamId;
    var filter = frame.Filter<Unit, Team, OwnerComponent, Combat>();
    while (filter.Next(out var entity)) {
      ref readonly var team = ref frame.GetReadOnly<Team>(entity);
      if (team.TeamId != targetTeamId)
        return entity;
    }

    throw new Xunit.Sdk.XunitException("Expected an enemy combat unit.");
  }

  private static EntityRef GetFirstHeroEntity(ref Frame frame) {
    var filter = frame.Filter<Player, Unit, Health, TransformComponent>();
    if (filter.Next(out var entity))
      return entity;

    throw new Xunit.Sdk.XunitException("Expected an initialized hero entity.");
  }

  private static EntityRef SpawnTestMinion(ref Frame frame) {
    var entity = frame.CreateEntity();
    int unitId = UnitIdGenerator.Next(ref frame);

    frame.Add(entity, TransformFactory.At(FPVector3.Zero));
    frame.Add(entity, new Unit {
      UnitId = unitId,
      UnitTypeId = SimulationSetup.MinionUnitTypeId,
    });
    frame.Add(entity, new Team { TeamId = 1 });
    frame.Add(entity, new OwnerComponent { OwnerId = 1 });
    frame.Add(entity, new Minion { WaveId = 99 });
    frame.Add(entity, new Health(100));

    return entity;
  }
}
