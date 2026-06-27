using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Meesles.Avalon.Sim.Commands;
using Meesles.Avalon.Sim.Models;
using Xunit;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Tests;

public class AttackCommandExecutionTests {
  [Fact]
  public void CommandFactory_CreatesAttackCommand() {
    WarmupRegistry.RunAll();
    var factory = new CommandFactory();
    factory.CreateCommand(AttackCommand.TYPE_ID).Should().BeOfType<AttackCommand>();
  }

  [Fact]
  public void MissingTarget_NoOps() {
    var harness = SimHarness.CreateInitialized();

    harness.Tick(SimHarness.AttackCommand(1, 0, targetUnitId: 999, sourceUnitIds: 3));

    HasAttackTarget(harness.Frame, unitId: 3).Should().BeFalse();
  }

  [Fact]
  public void DestroyedTarget_NoOps() {
    var harness = SimHarness.CreateInitialized();
    KillUnit(harness, unitId: 2);
    harness.Tick();

    harness.Tick(SimHarness.AttackCommand(1, 1, targetUnitId: 2, sourceUnitIds: 3));

    HasAttackTarget(harness.Frame, unitId: 3).Should().BeFalse();
  }

  [Fact]
  public void NonOwnedSource_NoOps() {
    var harness = SimHarness.CreateInitialized();

    harness.Tick(SimHarness.AttackCommand(1, 0, targetUnitId: 4, sourceUnitIds: 4));

    HasAttackTarget(harness.Frame, unitId: 4).Should().BeFalse();
  }

  [Fact]
  public void SameTeamTarget_NoOps() {
    var harness = SimHarness.CreateInitialized();

    harness.Tick(SimHarness.AttackCommand(1, 0, targetUnitId: 1, sourceUnitIds: 3));

    HasAttackTarget(harness.Frame, unitId: 3).Should().BeFalse();
  }

  [Fact]
  public void ValidAttack_RecordsIntentAndReplacesMoveTargetWithChaseTarget() {
    var harness = SimHarness.CreateInitialized();
    var (source, target) = SpawnFirstWave(harness);

    var move = SimHarness.MoveCommand(1, 0, FP64.One, -FP64.One);
    move.AddUnitId(source.UnitId);
    harness.Tick(move);

    HasMoveTarget(harness.Frame, source.UnitId).Should().BeTrue();

    harness.Tick(SimHarness.AttackCommand(1, 1, target.UnitId, source.UnitId));

    GetAttackTarget(harness.Frame, source.UnitId).Should().Be(target.UnitId);
    GetMoveTarget(harness.Frame, source.UnitId).Should().Be(target.Position);
  }

  [Fact]
  public void AttackIntent_UpdatesChaseTargetWhenTargetMoves() {
    var harness = SimHarness.CreateInitialized();
    var (source, target) = SpawnFirstWave(harness);
    harness.Tick(SimHarness.AttackCommand(1, 0, target.UnitId, source.UnitId));

    FPVector3 movedTarget = target.Position + new FPVector3(FP64.FromInt(3), FP64.Zero, FP64.FromInt(-2));
    SetPosition(harness, target.UnitId, movedTarget);

    harness.Tick();

    GetMoveTarget(harness.Frame, source.UnitId).Should().Be(movedTarget);
  }

  [Fact]
  public void AttackIntent_ClearsMoveTargetWhenSourceIsInRange() {
    var harness = SimHarness.CreateInitialized();
    var (source, target) = SpawnFirstWave(harness);
    FPVector3 targetPosition = target.Position;
    SetPosition(harness, source.UnitId, targetPosition + new FPVector3(FP64.One, FP64.Zero, FP64.Zero));
    SetMoveTarget(harness, source.UnitId, FPVector3.Zero);

    harness.Tick(SimHarness.AttackCommand(1, 0, target.UnitId, source.UnitId));

    HasMoveTarget(harness.Frame, source.UnitId).Should().BeFalse();
    GetAttackTarget(harness.Frame, source.UnitId).Should().Be(target.UnitId);
  }

  [Fact]
  public void AttackIntent_ClearsIntentWhenTargetDies() {
    var harness = SimHarness.CreateInitialized();
    var (source, target) = SpawnFirstWave(harness);
    harness.Tick(SimHarness.AttackCommand(1, 0, target.UnitId, source.UnitId));

    KillUnit(harness, target.UnitId);
    harness.Tick();

    HasAttackTarget(harness.Frame, source.UnitId).Should().BeFalse();
  }

  [Fact]
  public void AttackIntent_ReacquiresNearbyEnemyWhenTargetDies() {
    var harness = SimHarness.CreateInitialized();
    var (source, target) = SpawnFirstWave(harness);
    FPVector3 sourcePosition = new FPVector3(FP64.Zero, FP64.Zero, FP64.Zero);
    FPVector3 targetPosition = new FPVector3(FP64.FromInt(2), FP64.Zero, FP64.Zero);
    FPVector3 fallbackPosition = new FPVector3(FP64.FromInt(5), FP64.Zero, FP64.Zero);
    SetPosition(harness, source.UnitId, sourcePosition);
    SetPosition(harness, target.UnitId, targetPosition);
    SetPosition(harness, unitId: 2, fallbackPosition);
    harness.Tick(SimHarness.AttackCommand(1, 0, target.UnitId, source.UnitId));

    KillUnit(harness, target.UnitId);
    harness.Tick();

    GetAttackTarget(harness.Frame, source.UnitId).Should().Be(2);
    GetMoveTarget(harness.Frame, source.UnitId).Should().Be(fallbackPosition);
  }

  [Fact]
  public void AttackIntent_ClearsIntentWhenNoReacquireTargetIsInRadius() {
    var harness = SimHarness.CreateInitialized();
    var (source, target) = SpawnFirstWave(harness);
    SetPosition(harness, source.UnitId, FPVector3.Zero);
    SetPosition(harness, target.UnitId, new FPVector3(FP64.FromInt(2), FP64.Zero, FP64.Zero));
    SetPosition(harness, unitId: 2, new FPVector3(FP64.FromInt(20), FP64.Zero, FP64.Zero));
    harness.Tick(SimHarness.AttackCommand(1, 0, target.UnitId, source.UnitId));

    KillUnit(harness, target.UnitId);
    harness.Tick();

    HasAttackTarget(harness.Frame, source.UnitId).Should().BeFalse();
  }

  [Fact]
  public void AttackIntent_ClearsIntentForSourceWithoutCombat() {
    var harness = SimHarness.CreateInitialized();
    TryGetEntityByUnitId(harness.Frame, unitId: 3, out var source).Should().BeTrue();
    harness.Frame.Has<Combat>(source).Should().BeTrue();
    harness.Frame.Remove<Combat>(source);

    harness.Tick(SimHarness.AttackCommand(1, 0, targetUnitId: 4, sourceUnitIds: 3));

    HasAttackTarget(harness.Frame, unitId: 3).Should().BeFalse();
  }

  [Fact]
  public void HeroAttackCommand_SetsMoveTargetImmediately() {
    var harness = SimHarness.CreateInitialized();

    harness.Tick(SimHarness.AttackCommand(1, 0, targetUnitId: 2, sourceUnitIds: 3));

    HasAttackTarget(harness.Frame, unitId: 3).Should().BeTrue();
    FPVector3 targetPosition = GetPosition(harness.Frame, unitId: 2);
    GetMoveTarget(harness.Frame, unitId: 3)
      .Should()
      .Be(new FPVector3(targetPosition.x, FP64.Zero, targetPosition.z));
  }

  [Fact]
  public void HeroAttackCommand_DamagesEnemyWhenInRange() {
    var harness = SimHarness.CreateInitialized();
    FPVector3 targetPosition = GetPosition(harness.Frame, unitId: 2);
    SetPosition(harness, unitId: 3, targetPosition + new FPVector3(FP64.One, FP64.Zero, FP64.Zero));
    int startHealth = GetHealth(harness.Frame, unitId: 2);

    harness.Tick(SimHarness.AttackCommand(1, 0, targetUnitId: 2, sourceUnitIds: 3));

    GetHealth(harness.Frame, unitId: 2).Should().Be(startHealth - 10);
  }

  [Fact]
  public void DamageSystem_AppliesDamageAndStartsCooldownWhenTargetIsInRange() {
    var harness = SimHarness.CreateInitialized();
    var (source, target) = SpawnFirstWave(harness);
    SetPosition(harness, source.UnitId, target.Position + new FPVector3(FP64.One, FP64.Zero, FP64.Zero));

    int startHealth = GetHealth(harness.Frame, target.UnitId);

    harness.Tick(SimHarness.AttackCommand(1, 0, target.UnitId, source.UnitId));

    GetHealth(harness.Frame, target.UnitId).Should().Be(startHealth - 10);
    GetCooldown(harness.Frame, source.UnitId).Should().Be(30);
  }

  [Fact]
  public void AttackCooldownSystem_DecrementsCooldownAndPreventsRepeatedDamage() {
    var harness = SimHarness.CreateInitialized();
    var (source, target) = SpawnFirstWave(harness);
    SetPosition(harness, source.UnitId, target.Position + new FPVector3(FP64.One, FP64.Zero, FP64.Zero));

    harness.Tick(SimHarness.AttackCommand(1, 0, target.UnitId, source.UnitId));
    int healthAfterFirstHit = GetHealth(harness.Frame, target.UnitId);

    harness.Tick();

    GetHealth(harness.Frame, target.UnitId).Should().Be(healthAfterFirstHit);
    GetCooldown(harness.Frame, source.UnitId).Should().Be(29);
  }

  [Fact]
  public void DamageSystem_LethalDamageLetsDeathSystemRemoveTarget() {
    var harness = SimHarness.CreateInitialized();
    var (source, target) = SpawnFirstWave(harness);
    SetPosition(harness, unitId: 2, new FPVector3(FP64.FromInt(20), FP64.Zero, FP64.Zero));
    SetPosition(harness, unitId: 4, new FPVector3(FP64.FromInt(20), FP64.Zero, FP64.Zero));
    SetPosition(harness, source.UnitId, target.Position + new FPVector3(FP64.One, FP64.Zero, FP64.Zero));
    SetHealth(harness, target.UnitId, 10);

    harness.Tick(SimHarness.AttackCommand(1, 0, target.UnitId, source.UnitId));

    TryGetEntityByUnitId(harness.Frame, target.UnitId, out _).Should().BeFalse();

    harness.Tick();

    HasAttackTarget(harness.Frame, source.UnitId).Should().BeFalse();
  }

  [Fact]
  public void MoveCommand_ClearsAttackIntentAndTransientCombatTarget() {
    var harness = SimHarness.CreateInitialized();
    var (source, target) = SpawnFirstWave(harness);
    SetPosition(harness, source.UnitId, target.Position + new FPVector3(FP64.One, FP64.Zero, FP64.Zero));
    harness.Tick(SimHarness.AttackCommand(1, 0, target.UnitId, source.UnitId));

    HasCombatTarget(harness.Frame, source.UnitId).Should().BeTrue();

    var move = SimHarness.MoveCommand(1, 1, FP64.FromInt(5), FP64.FromInt(-5));
    move.AddUnitId(source.UnitId);
    harness.Tick(move);

    HasAttackTarget(harness.Frame, source.UnitId).Should().BeFalse();
    HasCombatTarget(harness.Frame, source.UnitId).Should().BeFalse();
  }

  private static bool HasAttackTarget(Frame frame, int unitId) {
    return TryGetEntityByUnitId(frame, unitId, out var entity)
        && frame.Has<AttackTargetUnitId>(entity);
  }

  private static bool HasMoveTarget(Frame frame, int unitId) {
    return TryGetEntityByUnitId(frame, unitId, out var entity)
        && frame.Has<UnitMoveTarget>(entity);
  }

  private static bool HasCombatTarget(Frame frame, int unitId) {
    return TryGetEntityByUnitId(frame, unitId, out var entity)
        && frame.Has<Combat>(entity)
        && frame.GetReadOnly<Combat>(entity).Target.IsValid;
  }

  private static int GetAttackTarget(Frame frame, int unitId) {
    TryGetEntityByUnitId(frame, unitId, out var entity).Should().BeTrue();
    frame.Has<AttackTargetUnitId>(entity).Should().BeTrue();
    return frame.GetReadOnly<AttackTargetUnitId>(entity).TargetUnitId;
  }

  private static FPVector3 GetMoveTarget(Frame frame, int unitId) {
    TryGetEntityByUnitId(frame, unitId, out var entity).Should().BeTrue();
    frame.Has<UnitMoveTarget>(entity).Should().BeTrue();
    return frame.GetReadOnly<UnitMoveTarget>(entity).Target;
  }

  private static FPVector3 GetPosition(Frame frame, int unitId) {
    TryGetEntityByUnitId(frame, unitId, out var entity).Should().BeTrue();
    frame.Has<TransformComponent>(entity).Should().BeTrue();
    return frame.GetReadOnly<TransformComponent>(entity).Position;
  }

  private static int GetHealth(Frame frame, int unitId) {
    TryGetEntityByUnitId(frame, unitId, out var entity).Should().BeTrue();
    frame.Has<Health>(entity).Should().BeTrue();
    return frame.GetReadOnly<Health>(entity).Current;
  }

  private static int GetCooldown(Frame frame, int unitId) {
    TryGetEntityByUnitId(frame, unitId, out var entity).Should().BeTrue();
    frame.Has<Combat>(entity).Should().BeTrue();
    return frame.GetReadOnly<Combat>(entity).CooldownRemainingTicks;
  }

  private static void KillUnit(SimHarness harness, int unitId) {
    TryGetEntityByUnitId(harness.Frame, unitId, out var entity).Should().BeTrue();
    harness.Frame.Has<Health>(entity).Should().BeTrue("only units with Health can be killed in this helper");
    ref var health = ref harness.Frame.Get<Health>(entity);
    health.Current = 0;
  }

  private static (UnitSnapshot Source, UnitSnapshot Target) SpawnFirstWave(SimHarness harness) {
    var rules = harness.AssetRegistry.Get<Meesles.Avalon.Sim.Assets.WaveRulesAsset>();
    for (int tick = 0; tick <= rules.FirstWaveDelayTicks; tick++)
      harness.Tick();

    var minions = GetMinions(harness);
    return (
      minions.Single(minion => minion.TeamId == 1),
      minions.Single(minion => minion.TeamId == 2));
  }

  private static UnitSnapshot[] GetMinions(SimHarness harness) {
    var frame = harness.Frame;
    var minions = new List<UnitSnapshot>();
    var filter = frame.Filter<Minion, Unit, Team, TransformComponent>();
    while (filter.Next(out var entity)) {
      ref readonly var unit = ref frame.GetReadOnly<Unit>(entity);
      ref readonly var team = ref frame.GetReadOnly<Team>(entity);
      ref readonly var transform = ref frame.GetReadOnly<TransformComponent>(entity);
      minions.Add(new UnitSnapshot(unit.UnitId, team.TeamId, transform.Position));
    }

    return minions.OrderBy(minion => minion.UnitId).ToArray();
  }

  private static void SetPosition(SimHarness harness, int unitId, FPVector3 position) {
    var frame = harness.Frame;
    TryGetEntityByUnitId(frame, unitId, out var entity).Should().BeTrue();
    frame.Has<TransformComponent>(entity).Should().BeTrue();
    ref var transform = ref frame.Get<TransformComponent>(entity);
    transform.Position = position;
  }

  private static void SetMoveTarget(SimHarness harness, int unitId, FPVector3 target) {
    var frame = harness.Frame;
    TryGetEntityByUnitId(frame, unitId, out var entity).Should().BeTrue();
    if (frame.Has<UnitMoveTarget>(entity)) {
      ref var moveTarget = ref frame.Get<UnitMoveTarget>(entity);
      moveTarget.Target = target;
      return;
    }

    frame.Add(entity, new UnitMoveTarget { Target = target });
  }

  private static void SetHealth(SimHarness harness, int unitId, int current) {
    var frame = harness.Frame;
    TryGetEntityByUnitId(frame, unitId, out var entity).Should().BeTrue();
    frame.Has<Health>(entity).Should().BeTrue();
    ref var health = ref frame.Get<Health>(entity);
    health.Current = current;
  }

  private static bool TryGetEntityByUnitId(Frame frame, int unitId, out EntityRef entity) {
    var filter = frame.Filter<Unit>();
    while (filter.Next(out entity)) {
      ref readonly var unit = ref frame.GetReadOnly<Unit>(entity);
      if (unit.UnitId == unitId)
        return true;
    }

    entity = default;
    return false;
  }

  private sealed record UnitSnapshot(int UnitId, int TeamId, FPVector3 Position);
}
