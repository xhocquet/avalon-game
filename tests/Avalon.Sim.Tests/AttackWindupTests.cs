using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Meesles.Avalon.Sim.Components;
using Meesles.Avalon.Sim.Factories;
using Xunit;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Tests;

// An auto-attack is a swing that lands later, not an instant hit. AttackWindup is the gap, and the
// two phase events around it are what attack animations hang off: starting a clip on the hit runs
// its wind-up after the blow.
public class AttackWindupTests {
  private const int AttackDamage = 10;
  private const float WindupSeconds = 0.16f; // 10 ticks at the harness's 16ms
  private const int WindupTicks = 10;

  [Fact]
  public void TheSwingLeadsTheHitByTheAuthoredWindup() {
    var harness = SimHarness.CreateInitialized();
    var attacker = SpawnDuel(harness, WindupSeconds);

    var log = RunAndCollect(harness, ticks: 20);

    var swing = log.Single(e => e.Event is AttackWindupStartedEvent);
    var hit = log.Single(e => e.Event is AttackHitEvent);
    (hit.Tick - swing.Tick).Should().Be(WindupTicks);
  }

  // The windup event carries the length so the view can size the clip to it rather than assuming a
  // tick rate it has no business knowing.
  [Fact]
  public void TheWindupEventReportsItsOwnLengthInSeconds() {
    var harness = SimHarness.CreateInitialized();
    SpawnDuel(harness, WindupSeconds);

    var swing = (AttackWindupStartedEvent)RunAndCollect(harness, ticks: 20)
      .First(e => e.Event is AttackWindupStartedEvent).Event;

    swing.WindupSeconds.ToFloat().Should().BeApproximately(WindupSeconds, 0.001f);
  }

  // One id across the whole swing is what lets a listener match the hit back to the wind-up it
  // already started an animation for - and what AttackProcConsumedEvent points at.
  [Fact]
  public void TheWindupAndTheHitShareOneAttackHitId() {
    var harness = SimHarness.CreateInitialized();
    SpawnDuel(harness, WindupSeconds);

    var log = RunAndCollect(harness, ticks: 20);

    var swing = (AttackWindupStartedEvent)log.Single(e => e.Event is AttackWindupStartedEvent).Event;
    var hit = (AttackHitEvent)log.Single(e => e.Event is AttackHitEvent).Event;
    swing.AttackHitId.Should().Be(hit.AttackHitId);
    swing.AttackHitId.Should().NotBe(0);
  }

  [Fact]
  public void ATargetThatDiesMidSwing_CancelsInsteadOfLanding() {
    var harness = SimHarness.CreateInitialized();
    var attacker = SpawnDuel(harness, WindupSeconds);

    var log = RunAndCollect(harness, ticks: 20, onTick: (h, tick) => {
      if (h.HasSwingInFlight(UnitId(h, attacker)))
        KillTarget(h);
    });

    log.Should().NotContain(e => e.Event is AttackHitEvent, "the target was gone before the blow landed");
    log.Should().Contain(e => e.Event is AttackWindupCanceledEvent);
  }

  [Fact]
  public void ATargetThatLeavesRangeMidSwing_CancelsInsteadOfLanding() {
    var harness = SimHarness.CreateInitialized();
    var attacker = SpawnDuel(harness, WindupSeconds);

    var log = RunAndCollect(harness, ticks: 20, onTick: (h, tick) => {
      if (h.HasSwingInFlight(UnitId(h, attacker)))
        MoveTargetOutOfRange(h);
    });

    log.Should().NotContain(e => e.Event is AttackHitEvent);
    log.Should().Contain(e => e.Event is AttackWindupCanceledEvent);
  }

  // The period is paid at the swing, so a whiff is not a free re-roll.
  [Fact]
  public void AWhiffedSwing_StillCostsTheAttackPeriod() {
    var harness = SimHarness.CreateInitialized();
    var attacker = SpawnDuel(harness, WindupSeconds);

    RunAndCollect(harness, ticks: 20, onTick: (h, tick) => {
      if (h.HasSwingInFlight(UnitId(h, attacker)))
        MoveTargetOutOfRange(h);
    });

    harness.Frame.GetReadOnly<Combat>(attacker).CooldownRemainingTicks.Should().BePositive();
  }

  // Nothing authored a wind-up, so the swing is the hit - the cadence a unit had before wind-up
  // existed at all.
  [Fact]
  public void WithNoAuthoredWindup_TheHitLandsOnTheSwingTick() {
    var harness = SimHarness.CreateInitialized();
    SpawnDuel(harness, windupSeconds: 0.0f);

    var log = RunAndCollect(harness, ticks: 20);

    var swing = log.Single(e => e.Event is AttackWindupStartedEvent);
    var hit = log.Single(e => e.Event is AttackHitEvent);
    hit.Tick.Should().Be(swing.Tick);
  }

  // --- helpers ---

  private static List<(int Tick, SimulationEvent Event)> RunAndCollect(SimHarness harness, int ticks,
    System.Action<SimHarness, int> onTick = null) {
    var log = new List<(int, SimulationEvent)>();

    for (var i = 0; i < ticks; i++) {
      var collector = new EventCollector();
      var frame = harness.Frame;
      collector.BeginTick(frame.Tick);
      frame.EventRaiser = collector;

      onTick?.Invoke(harness, i);
      harness.Tick();

      for (var e = 0; e < collector.Count; e++)
        log.Add((collector.Collected[e].Tick, collector.Collected[e]));
    }

    return log;
  }

  private static int UnitId(SimHarness harness, EntityRef entity) {
    return harness.Frame.GetReadOnly<UnitIdComponent>(entity).UnitId;
  }

  private static void KillTarget(SimHarness harness) {
    var frame = harness.Frame;
    var filter = frame.Filter<Minion, Health, TeamComponent>();
    while (filter.Next(out var entity)) {
      if (frame.GetReadOnly<TeamComponent>(entity).TeamId == 2)
        frame.Get<Health>(entity).Current = FP64.Zero;
    }
  }

  private static void MoveTargetOutOfRange(SimHarness harness) {
    var frame = harness.Frame;
    var filter = frame.Filter<Minion, TransformComponent, TeamComponent>();
    while (filter.Next(out var entity)) {
      if (frame.GetReadOnly<TeamComponent>(entity).TeamId == 2)
        frame.Get<TransformComponent>(entity).Position = new FPVector3(FP64.FromInt(60), FP64.Zero, FP64.Zero);
    }
  }

  // Two hostile minions a unit apart, well inside the attacker's reach, the way AttackSpeedTests
  // builds one: hand-rolled so the attacker holds still and the target cannot fight back.
  private static EntityRef SpawnDuel(SimHarness harness, float windupSeconds) {
    var frame = harness.Frame;

    var attacker = SpawnMinion(ref frame, FPVector3.Zero, teamId: 1, health: 100);
    frame.Add(attacker, StatsComponent.Create()
      .With(StatType.AttackDamage, FP64.FromInt(AttackDamage))
      .With(StatType.BaseAttackSpeed, FP64.One)
      .With(StatType.AttackRange, FP64.FromInt(3))
      .With(StatType.AcquisitionRange, FP64.FromInt(9))
      .With(StatType.AttackWindup, FP64.FromFloat(windupSeconds)));
    frame.Add(attacker, new Combat());

    var target = SpawnMinion(ref frame, new FPVector3(FP64.One, FP64.Zero, FP64.Zero), teamId: 2,
      health: 10000);
    frame.Add(target, StatsComponent.Create()
      .With(StatType.MaxHealth, FP64.FromInt(10000))
      .With(StatType.AttackDamage, FP64.Zero));

    return attacker;
  }

  private static EntityRef SpawnMinion(ref Frame frame, FPVector3 position, int teamId, int health) {
    var entity = frame.CreateEntity();
    frame.Add(entity, TransformFactory.At(position));
    frame.Add(entity, new UnitIdComponent {
      UnitId = UnitLookup.NextUnitId(ref frame),
      UnitTypeId = SimulationSetup.MinionUnitTypeId
    });
    frame.Add(entity, new TeamComponent { TeamId = teamId });
    frame.Add(entity, new Minion { WaveId = 99 });
    frame.Add(entity, new Health(FP64.FromInt(health)));

    return entity;
  }
}
