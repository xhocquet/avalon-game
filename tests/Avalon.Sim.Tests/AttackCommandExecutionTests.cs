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
  public void ValidAttack_RecordsIntentAndClearsMoveTarget() {
    var harness = SimHarness.CreateInitialized();
    harness.Tick(SimHarness.MoveCommand(1, 0, FP64.One, -FP64.One));

    HasMoveTarget(harness.Frame, unitId: 3).Should().BeTrue();

    harness.Tick(SimHarness.AttackCommand(1, 1, targetUnitId: 4, sourceUnitIds: 3));

    HasMoveTarget(harness.Frame, unitId: 3).Should().BeFalse();
    GetAttackTarget(harness.Frame, unitId: 3).Should().Be(4);
  }

  private static bool HasAttackTarget(Frame frame, int unitId) {
    return TryGetEntityByUnitId(frame, unitId, out var entity)
        && frame.Has<AttackTargetUnitId>(entity);
  }

  private static bool HasMoveTarget(Frame frame, int unitId) {
    return TryGetEntityByUnitId(frame, unitId, out var entity)
        && frame.Has<UnitMoveTarget>(entity);
  }

  private static int GetAttackTarget(Frame frame, int unitId) {
    TryGetEntityByUnitId(frame, unitId, out var entity).Should().BeTrue();
    frame.Has<AttackTargetUnitId>(entity).Should().BeTrue();
    return frame.GetReadOnly<AttackTargetUnitId>(entity).TargetUnitId;
  }

  private static void KillUnit(SimHarness harness, int unitId) {
    TryGetEntityByUnitId(harness.Frame, unitId, out var entity).Should().BeTrue();
    harness.Frame.Has<Health>(entity).Should().BeTrue("only units with Health can be killed in this helper");
    ref var health = ref harness.Frame.Get<Health>(entity);
    health.Current = 0;
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
}
