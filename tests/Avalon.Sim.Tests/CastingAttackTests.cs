using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using Meesles.Avalon.Sim.Factories;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using Xunit;

namespace Meesles.Avalon.Sim.Tests;

public class CastingAttackTests {
  [Fact]
  public void BadHairDay_CancelsPendingSwingAndPreventsAttacksUntilItCompletes() {
    var harness = SimHarness.CreateInitialized(maxPlayers: 1);
    PrepareDuel(harness);
    var hero = harness.FindHero(1);
    var frame = harness.Frame;
    var unitId = UnitLookup.GetUnitId(ref frame, hero);
    var opening = Tick(harness);
    opening.OfType<AttackWindupStartedEvent>().Should().Contain(e => e.AttackerUnitId == unitId);
    frame.GetReadOnly<Combat>(hero).WindupReleaseTick.Should().BePositive();
    var cast = Tick(harness, SimHarness.CastSkillCommand(1, frame.Tick, 3));
    cast.OfType<AttackWindupCanceledEvent>().Should().ContainSingle(e => e.AttackerUnitId == unitId);
    frame.GetReadOnly<Combat>(hero).WindupReleaseTick.Should().Be(0);
    var detonateTick = frame.GetReadOnly<SkillCharge>(hero).DetonateTick;
    while (harness.Frame.Tick < detonateTick) {
      var events = Tick(harness);
      events.OfType<AttackWindupStartedEvent>().Should().NotContain(e => e.AttackerUnitId == unitId);
      frame.GetReadOnly<Combat>(hero).WindupReleaseTick.Should().Be(0);
    }
    var after = new List<SimulationEvent>();
    for (var i = 0; i < 100; i++) after.AddRange(Tick(harness));
    after.OfType<AttackWindupStartedEvent>().Should().Contain(e => e.AttackerUnitId == unitId);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public void TimedCasts_BlockAcquisitionPursuitAndAttacks_UntilExplicitlyCleared(bool channel) {
    var harness = SimHarness.CreateInitialized(maxPlayers: 1);
    PrepareDuel(harness);
    var frame = harness.Frame;
    var hero = harness.FindHero(1);
    var position = frame.GetReadOnly<TransformComponent>(hero).Position;
    var target = TrainingDummyFactory.Spawn(ref frame,
      position + FPVector3.Right * FP64.FromInt(10), 2, AssetIds.FactionCrystalWarriors);
    var targetId = UnitLookup.GetUnitId(ref frame, target);
    UnitIntentController.SetAttackTarget(ref frame, hero, targetId);
    if (channel)
      ChannelController.Arm(ref frame, hero,
        frame.AssetRegistry.Get<SkillAsset>(AssetIds.SkillSnailheadUltimate), 1, position).Should().BeTrue();
    else
      ChargeController.Arm(ref frame, hero, AssetIds.SkillHairyWizardUltimate, 120,
        FP64.Zero, FP64.FromInt(10), 0).Should().BeTrue();
    new AttackIntentSystem().Update(ref frame);
    frame.Has<UnitMoveTarget>(hero).Should().BeFalse();
    UnitIntentController.ClearAttackIntent(ref frame, hero);
    new TargetAcquisitionSystem().Update(ref frame);
    frame.Has<AttackTargetUnitId>(hero).Should().BeFalse();
    UnitIntentController.SetAttackTarget(ref frame, hero, targetId);
    frame.Get<Combat>(hero).TargetUnitId = targetId;
    frame.Get<TransformComponent>(target).Position = position + FPVector3.Right;
    new DamageSystem().Update(ref frame);
    frame.GetReadOnly<Combat>(hero).WindupReleaseTick.Should().Be(0);
    if (channel) ChannelController.Clear(ref frame, hero);
    else ChargeController.Clear(ref frame, hero);
    new AttackIntentSystem().Update(ref frame);
    new DamageSystem().Update(ref frame);
    frame.GetReadOnly<Combat>(hero).WindupReleaseTick.Should().BePositive();
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public void StartingTimedCast_CancelsWindupWithoutSpendingEmpoweredAttack(bool channel) {
    var harness = SimHarness.CreateInitialized(maxPlayers: 1);
    PrepareDuel(harness);
    Tick(harness);
    var frame = harness.Frame;
    var hero = harness.FindHero(1);
    var cooldown = frame.GetReadOnly<Combat>(hero).CooldownRemainingTicks;
    cooldown.Should().BePositive();
    EmpoweredAttackController.Arm(ref frame, hero, 508, FP64.FromInt(2), 1000);
    var events = new EventCollector();
    events.BeginTick(frame.Tick);
    frame.EventRaiser = events;
    if (channel)
      ChannelController.Arm(ref frame, hero,
        frame.AssetRegistry.Get<SkillAsset>(AssetIds.SkillSnailheadUltimate), 1,
        frame.GetReadOnly<TransformComponent>(hero).Position);
    else
      ChargeController.Arm(ref frame, hero, 503, 120, FP64.Zero, FP64.FromInt(10), 0);
    frame.GetReadOnly<Combat>(hero).WindupReleaseTick.Should().Be(0);
    frame.GetReadOnly<Combat>(hero).CooldownRemainingTicks.Should().Be(cooldown);
    frame.GetReadOnly<AttackProc>(hero).IsArmed.Should().BeTrue();
    events.Collected.OfType<AttackWindupCanceledEvent>().Should().ContainSingle();
  }

  [Fact]
  public void MispredictedCast_RestoresPendingSwingAndReplaysDeterministically() {
    var rollback = RollbackHarness.Create(maxPlayers: 1);
    PrepareDuel(rollback.Server);
    PrepareDuel(rollback.Client);
    rollback.Advance(1);
    var frame = rollback.Client.Frame;
    var hero = rollback.Client.FindHero(1);
    var swing = frame.GetReadOnly<Combat>(hero).WindupAttackHitId;
    swing.Should().BePositive();
    var castTick = rollback.Tick;
    rollback.MispredictAndRollback(10,
      tick => tick == castTick ? [SimHarness.CastSkillCommand(1, tick, 3)] : []);
    frame = rollback.Client.Frame;
    frame.GetReadOnly<Combat>(hero).WindupAttackHitId.Should().Be(swing);
    SkillCasting.IsActive(ref frame, hero).Should().BeFalse();
    rollback.AdvanceAndCompare(220).Should().BeEmpty();
  }

  private static void PrepareDuel(SimHarness harness) {
    var frame = harness.Frame;
    var hero = harness.FindHero(1);
    var others = new List<EntityRef>();
    var combatants = frame.Filter<Combat>();
    while (combatants.Next(out var entity))
      if (entity != hero) others.Add(entity);
    foreach (var entity in others) frame.Remove<Combat>(entity);
    frame.Get<Skills>(hero).TrySpendPoint(3, 4).Should().BeTrue();
    var position = frame.GetReadOnly<TransformComponent>(hero).Position;
    TrainingDummyFactory.Spawn(ref frame, position + FPVector3.Right, 2, AssetIds.FactionCrystalWarriors);
  }

  private static SimulationEvent[] Tick(SimHarness harness, params ICommand[] commands) {
    var collector = new EventCollector();
    collector.BeginTick(harness.Frame.Tick);
    harness.Frame.EventRaiser = collector;
    harness.Tick(commands);
    return collector.Collected.ToArray();
  }
}
