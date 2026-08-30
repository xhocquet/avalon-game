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
    int unitId = frame.GetReadOnly<UnitIdentity>(crystal).UnitId;
    int crystalId = frame.GetReadOnly<Crystal>(crystal).CrystalId;
    int teamId = frame.GetReadOnly<Team>(crystal).TeamId;
    int destroyerUnitId = frame.GetReadOnly<UnitIdentity>(attacker).UnitId;
    int destroyerTeamId = frame.GetReadOnly<Team>(attacker).TeamId;
    ref var crystalHealth = ref frame.Get<Health>(crystal);
    crystalHealth.Current = 0;
    crystalHealth.LastDamagerUnitId = destroyerUnitId;

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
    evt.DestroyerUnitId.Should().Be(destroyerUnitId);
    evt.DestroyerTeamId.Should().Be(destroyerTeamId);
  }

  [Fact]
  public void Update_CreditsKillingBlowNotLowestUnitIdAttacker() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    EntityRef crystal = GetFirstCrystalEntity(ref frame);
    int crystalTeamId = frame.GetReadOnly<Team>(crystal).TeamId;
    int enemyTeamId = crystalTeamId + 1;

    EntityRef bystander = SpawnTestAttacker(ref frame, enemyTeamId);
    EntityRef killer = SpawnTestAttacker(ref frame, enemyTeamId);
    int killerUnitId = frame.GetReadOnly<UnitIdentity>(killer).UnitId;
    frame.GetReadOnly<UnitIdentity>(bystander).UnitId.Should().BeLessThan(killerUnitId);

    // Both hold the corpse as their target; only the killer landed the fatal hit.
    int crystalUnitId = frame.GetReadOnly<UnitIdentity>(crystal).UnitId;
    frame.Get<Combat>(bystander).TargetUnitId = crystalUnitId;
    frame.Get<Combat>(killer).TargetUnitId = crystalUnitId;
    ref var health = ref frame.Get<Health>(crystal);
    health.Current = 0;
    health.LastDamagerUnitId = killerUnitId;

    var collector = new EventCollector();
    collector.BeginTick(7);
    frame.EventRaiser = collector;

    var system = new DeathSystem();
    system.Update(ref frame);

    var evt = collector.Collected[0].Should().BeOfType<CrystalDestroyedEvent>().Subject;
    evt.DestroyerUnitId.Should().Be(killerUnitId);
    evt.DestroyerTeamId.Should().Be(enemyTeamId);
  }

  [Fact]
  public void Update_LeavesDestroyerUnresolvedWhenNothingDealtDamage() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    EntityRef crystal = GetFirstCrystalEntity(ref frame);
    frame.Get<Health>(crystal).Current = 0;

    var collector = new EventCollector();
    collector.BeginTick(7);
    frame.EventRaiser = collector;

    var system = new DeathSystem();
    system.Update(ref frame);

    var evt = collector.Collected[0].Should().BeOfType<CrystalDestroyedEvent>().Subject;
    evt.DestroyerUnitId.Should().Be(0);
    evt.DestroyerTeamId.Should().Be(0);
  }

  [Fact]
  public void Update_RemovesDeadTurretAndRaisesTurretDestroyedEvent() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    EntityRef turret = GetFirstTurretEntity(ref frame);
    EntityRef attacker = GetEnemyCombatUnit(ref frame, turret);
    int unitId = frame.GetReadOnly<UnitIdentity>(turret).UnitId;
    int destroyerUnitId = frame.GetReadOnly<UnitIdentity>(attacker).UnitId;
    ref var turretHealth = ref frame.Get<Health>(turret);
    turretHealth.Current = 0;
    turretHealth.LastDamagerUnitId = destroyerUnitId;

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
    int unitId = frame.GetReadOnly<UnitIdentity>(entity).UnitId;
    int unitTypeId = frame.GetReadOnly<UnitIdentity>(entity).UnitTypeId;
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
    int unitId = frame.GetReadOnly<UnitIdentity>(entity).UnitId;
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
    int unitId = frame.GetReadOnly<UnitIdentity>(entity).UnitId;
    frame.Get<Health>(entity).Current = 0;

    var system = new DeathSystem();
    system.Update(ref frame);

    UnitLookup.TryGetEntityByUnitId(ref frame, unitId, out var resolved).Should().BeTrue();
    resolved.Should().Be(entity);
  }

  private static EntityRef GetFirstCrystalEntity(ref Frame frame) {
    var filter = frame.Filter<Crystal, UnitIdentity, Team, Health, TransformComponent>();
    if (filter.Next(out var entity))
      return entity;

    throw new Xunit.Sdk.XunitException("Expected an initialized crystal entity.");
  }

  private static EntityRef GetFirstTurretEntity(ref Frame frame) {
    var filter = frame.Filter<Turret, UnitIdentity, Health, TransformComponent>();
    if (filter.Next(out var entity))
      return entity;

    throw new Xunit.Sdk.XunitException("Expected an initialized turret entity.");
  }

  private static EntityRef GetEnemyCombatUnit(ref Frame frame, EntityRef target) {
    int targetTeamId = frame.GetReadOnly<Team>(target).TeamId;
    var filter = frame.Filter<UnitIdentity, Team, Combat>();
    while (filter.Next(out var entity)) {
      ref readonly var team = ref frame.GetReadOnly<Team>(entity);
      if (team.TeamId != targetTeamId)
        return entity;
    }

    throw new Xunit.Sdk.XunitException("Expected an enemy combat unit.");
  }

  private static EntityRef GetFirstHeroEntity(ref Frame frame) {
    var filter = frame.Filter<Player, UnitIdentity, Health, TransformComponent>();
    if (filter.Next(out var entity))
      return entity;

    throw new Xunit.Sdk.XunitException("Expected an initialized hero entity.");
  }

  private static EntityRef SpawnTestAttacker(ref Frame frame, int teamId) {
    var entity = frame.CreateEntity();

    frame.Add(entity, TransformFactory.At(FPVector3.Zero));
    frame.Add(entity, new UnitIdentity {
      UnitId = UnitLookup.NextUnitId(ref frame),
      UnitTypeId = SimulationSetup.MinionUnitTypeId,
    });
    frame.Add(entity, new Team { TeamId = teamId });
    frame.Add(entity, new Minion { WaveId = 99 });
    frame.Add(entity, new Health(100));
    frame.Add(entity, new Combat());
    frame.Add(entity, Stats.Create()
      .With(StatType.AttackRange, FP64.FromInt(2))
      .With(StatType.AcquisitionRange, FP64.FromInt(6)));

    return entity;
  }

  private static EntityRef SpawnTestMinion(ref Frame frame) {
    var entity = frame.CreateEntity();
    int unitId = UnitLookup.NextUnitId(ref frame);

    frame.Add(entity, TransformFactory.At(FPVector3.Zero));
    frame.Add(entity, new UnitIdentity {
      UnitId = unitId,
      UnitTypeId = SimulationSetup.MinionUnitTypeId,
    });
    frame.Add(entity, new Team { TeamId = 1 });
    frame.Add(entity, new Minion { WaveId = 99 });
    frame.Add(entity, new Health(100));

    return entity;
  }
}
