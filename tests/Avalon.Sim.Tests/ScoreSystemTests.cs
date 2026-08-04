using FluentAssertions;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using Xunit;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Tests;

public class ScoreSystemTests {
  [Fact]
  public void Update_AfterCrystalDestructionRaisesGameOverWhenOnePlayerTeamRemains() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    EntityRef crystal = GetCrystalForTeam(ref frame, teamId: 1);
    frame.Get<Health>(crystal).Current = 0;

    var collector = new EventCollector();
    collector.BeginTick(7);
    frame.EventRaiser = collector;

    new DeathSystem().Update(ref frame);
    var system = new ScoreSystem();
    system.Update(ref frame);

    collector.Count.Should().Be(2);
    collector.Collected[0].Should().BeOfType<CrystalDestroyedEvent>();
    var gameOver = collector.Collected[1].Should().BeOfType<GameOverEvent>().Subject;
    gameOver.Tick.Should().Be(7);

    ref readonly var matchEnd = ref frame.GetReadOnlySingleton<MatchEndStateComponent>();
    matchEnd.Ended.Should().BeTrue();
    matchEnd.WinnerPlayerId.Should().Be(2);

    MatchResultReader.TryRead(ref frame, 7, out var result).Should().BeTrue();
    result.EndTick.Should().Be(7);
    result.WinnerPlayerId.Should().Be(2);
    result.WinnerTeamId.Should().Be(2);
    result.Reason.Should().Be(MatchEndReason.Crystal);
  }

  [Fact]
  public void Update_AfterCrystalDestructionDoesNotEndMatchWhenMultiplePlayerTeamsRemain() {
    var harness = SimHarness.CreateInitialized(maxPlayers: 3);
    var frame = harness.Frame;
    EntityRef crystal = GetCrystalForTeam(ref frame, teamId: 1);
    frame.Get<Health>(crystal).Current = 0;

    var collector = new EventCollector();
    collector.BeginTick(7);
    frame.EventRaiser = collector;

    new DeathSystem().Update(ref frame);
    var system = new ScoreSystem();
    system.Update(ref frame);

    collector.Count.Should().Be(1);
    collector.Collected[0].Should().BeOfType<CrystalDestroyedEvent>();

    ref readonly var matchEnd = ref frame.GetReadOnlySingleton<MatchEndStateComponent>();
    matchEnd.Ended.Should().BeFalse();
    matchEnd.WinnerPlayerId.Should().Be(-1);
  }

  [Fact]
  public void Update_DoesNotRaiseGameOverMoreThanOnce() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    EntityRef crystal = GetCrystalForTeam(ref frame, teamId: 1);
    frame.Get<Health>(crystal).Current = 0;

    var collector = new EventCollector();
    collector.BeginTick(7);
    frame.EventRaiser = collector;

    new DeathSystem().Update(ref frame);
    var system = new ScoreSystem();
    system.Update(ref frame);

    collector.Collected.Should().ContainSingle(evt => evt is GameOverEvent);

    collector.BeginTick(8);
    system.Update(ref frame);

    collector.Count.Should().Be(0);
    ref readonly var matchEnd = ref frame.GetReadOnlySingleton<MatchEndStateComponent>();
    matchEnd.Ended.Should().BeTrue();
    matchEnd.WinnerPlayerId.Should().Be(2);
  }

  [Fact]
  public void Update_OnTimeoutRaisesGameOverAndStoresDrawEndState() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    frame.Tick = GetTimeoutTick(ref frame);

    var collector = new EventCollector();
    collector.BeginTick(frame.Tick);
    frame.EventRaiser = collector;

    new ScoreSystem().Update(ref frame);

    collector.Count.Should().Be(1);
    collector.Collected[0].Should().BeOfType<GameOverEvent>();

    ref readonly var matchEnd = ref frame.GetReadOnlySingleton<MatchEndStateComponent>();
    matchEnd.Ended.Should().BeTrue();
    matchEnd.WinnerPlayerId.Should().Be(-1);

    MatchResultReader.TryRead(ref frame, frame.Tick, out var result).Should().BeTrue();
    result.WinnerPlayerId.Should().Be(MatchResult.NoWinnerPlayerId);
    result.WinnerTeamId.Should().Be(MatchResult.NoWinnerTeamId);
    result.Reason.Should().Be(MatchEndReason.Timeout);
  }

  [Fact]
  public void Update_OnTickPastTimeoutStillEndsMatch() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    // The exact timeout tick can be missed (a rollback resimulation that starts past it, a system
    // ordering change); the check is >= so a skipped tick can't leave the match running forever.
    frame.Tick = GetTimeoutTick(ref frame) + 5;

    var collector = new EventCollector();
    collector.BeginTick(frame.Tick);
    frame.EventRaiser = collector;

    new ScoreSystem().Update(ref frame);

    collector.Count.Should().Be(1);
    collector.Collected[0].Should().BeOfType<GameOverEvent>();

    ref readonly var matchEnd = ref frame.GetReadOnlySingleton<MatchEndStateComponent>();
    matchEnd.Ended.Should().BeTrue();
    matchEnd.WinnerPlayerId.Should().Be(-1);

    MatchResultReader.TryRead(ref frame, frame.Tick, out var result).Should().BeTrue();
    result.Reason.Should().Be(MatchEndReason.Timeout);
  }

  [Fact]
  public void Update_WithZeroDeltaTimeFallsBackToDefaultTickLength() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var timeoutTickAt16Ms = GetTimeoutTick(ref frame);
    frame.DeltaTimeMs = 0;

    var collector = new EventCollector();
    frame.EventRaiser = collector;

    frame.Tick = timeoutTickAt16Ms - 1;
    collector.BeginTick(frame.Tick);
    new ScoreSystem().Update(ref frame);

    collector.Count.Should().Be(0);
    frame.GetReadOnlySingleton<MatchEndStateComponent>().Ended.Should().BeFalse();

    frame.Tick = timeoutTickAt16Ms;
    collector.BeginTick(frame.Tick);
    new ScoreSystem().Update(ref frame);

    collector.Count.Should().Be(1);
    collector.Collected[0].Should().BeOfType<GameOverEvent>();
    frame.GetReadOnlySingleton<MatchEndStateComponent>().Ended.Should().BeTrue();
  }

  private static int GetTimeoutTick(ref Frame frame) {
    var rules = frame.AssetRegistry.Get<MatchRulesAsset>();
    var matchDurationMs = (rules.MatchDuration * xpTURN.Klotho.Deterministic.Math.FP64.FromInt(1000)).ToInt();
    var deltaTimeMs = frame.DeltaTimeMs > 0 ? frame.DeltaTimeMs : SimHarness.DefaultDeltaTimeMs;
    return matchDurationMs / deltaTimeMs;
  }

  private static EntityRef GetCrystalForTeam(ref Frame frame, int teamId) {
    var filter = frame.Filter<Crystal, TeamComponent, Health>();
    while (filter.Next(out var entity)) {
      ref readonly var team = ref frame.GetReadOnly<TeamComponent>(entity);
      if (team.TeamId == teamId)
        return entity;
    }

    throw new Xunit.Sdk.XunitException($"Expected crystal for team {teamId}.");
  }
}
