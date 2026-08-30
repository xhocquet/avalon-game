using FluentAssertions;
using Meesles.Avalon.Sim.Components;
using Meesles.Avalon.Sim.Factories;
using Xunit;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Tests;

// TargetAcquisitionSystem ranks candidates by unit-type priority, then by distance, with UnitId only
// breaking exact distance ties.
public class TargetAcquisitionTests {
  [Fact]
  public void AcquiresTheNearestCandidateRatherThanTheOldest() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;

    var attacker = SpawnAttacker(ref frame);
    var far = SpawnTarget(ref frame, FP64.FromInt(4));
    var near = SpawnTarget(ref frame, FP64.FromInt(1));

    harness.Tick();

    var farUnitId = frame.GetReadOnly<UnitIdentity>(far).UnitId;
    var nearUnitId = frame.GetReadOnly<UnitIdentity>(near).UnitId;
    nearUnitId.Should().BeGreaterThan(farUnitId); // The nearer minion is the newer one.
    harness.Frame.GetReadOnly<AttackTargetUnitId>(attacker).TargetUnitId.Should().Be(nearUnitId);
  }

  // Type priority still outranks distance: a closer turret loses to a further minion.
  [Fact]
  public void PriorityStillOutranksDistance() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;

    var attacker = SpawnAttacker(ref frame);
    var turret = SpawnTarget(ref frame, FP64.One, isTurret: true);
    var minion = SpawnTarget(ref frame, FP64.FromInt(4));

    harness.Tick();

    var targetUnitId = harness.Frame.GetReadOnly<AttackTargetUnitId>(attacker).TargetUnitId;
    targetUnitId.Should().Be(frame.GetReadOnly<UnitIdentity>(minion).UnitId);
    targetUnitId.Should().NotBe(frame.GetReadOnly<UnitIdentity>(turret).UnitId);
  }

  // Reacquisition belongs to AttackIntentSystem, so an attacker holding a target keeps it even once a
  // closer candidate shows up.
  [Fact]
  public void DoesNotRetargetAnAttackerThatAlreadyHasATarget() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;

    var attacker = SpawnAttacker(ref frame);
    var far = SpawnTarget(ref frame, FP64.FromInt(4));

    harness.Tick();

    frame = harness.Frame;
    var farUnitId = frame.GetReadOnly<UnitIdentity>(far).UnitId;
    frame.GetReadOnly<AttackTargetUnitId>(attacker).TargetUnitId.Should().Be(farUnitId);

    SpawnTarget(ref frame, FP64.One);
    harness.Tick();

    harness.Frame.GetReadOnly<AttackTargetUnitId>(attacker).TargetUnitId.Should().Be(farUnitId);
  }

  private static EntityRef SpawnAttacker(ref Frame frame) {
    var attacker = SpawnUnit(ref frame, FPVector3.Zero, teamId: 1);
    frame.Add(attacker, Stats.Create()
      .With(StatType.AttackDamage, FP64.FromInt(10))
      .With(StatType.AttackRange, FP64.FromInt(3))
      .With(StatType.AcquisitionRange, FP64.FromInt(9)));
    frame.Add(attacker, new Combat());

    return attacker;
  }

  private static EntityRef SpawnTarget(ref Frame frame, FP64 distance, bool isTurret = false) {
    var target = SpawnUnit(ref frame, new FPVector3(distance, FP64.Zero, FP64.Zero), teamId: 2, isTurret);
    frame.Add(target, Stats.Create().With(StatType.AttackDamage, FP64.Zero));

    return target;
  }

  private static EntityRef SpawnUnit(ref Frame frame, FPVector3 position, int teamId, bool isTurret = false) {
    var entity = frame.CreateEntity();
    frame.Add(entity, TransformFactory.At(position));
    frame.Add(entity, new UnitIdentity {
      UnitId = UnitLookup.NextUnitId(ref frame),
      UnitTypeId = SimulationSetup.MinionUnitTypeId
    });
    frame.Add(entity, new Team { TeamId = teamId });
    if (isTurret)
      frame.Add(entity, new Turret());
    else
      frame.Add(entity, new Minion { WaveId = 99 });
    frame.Add(entity, new Health(FP64.FromInt(10000)));

    return entity;
  }
}
