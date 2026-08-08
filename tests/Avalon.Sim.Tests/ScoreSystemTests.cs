using FluentAssertions;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using Xunit;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Tests;

public class ScoreSystemTests {
  [Fact]
  public void Update_AfterCrystalDestructionRaisesGameOverWhenOneCrystalTeamRemains() {
    var harness = SimHarness.CreateInitialized();
    harness.CompleteMatchSetup();
    var frame = harness.Frame;
    EntityRef crystal = GetCrystalForTeam(ref frame, teamId: 1);
    frame.Get<Health>(crystal).Current = 0;

    frame.Tick = 7;
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
    gameOver.WinnerTeamId.Should().Be(2);
    gameOver.WinnerPlayerId.Should().Be(2);
    gameOver.Reason.Should().Be((int)MatchEndReason.Crystal);

    ref readonly var outcome = ref frame.GetReadOnlySingleton<MatchOutcome>();
    outcome.Ended.Should().BeTrue();
    outcome.WinnerTeamId.Should().Be(2);
    outcome.Reason.Should().Be((int)MatchEndReason.Crystal);

    // Klotho's own end state still gets the single-winner view it needs.
    ref readonly var matchEnd = ref frame.GetReadOnlySingleton<MatchEndStateComponent>();
    matchEnd.Ended.Should().BeTrue();
    matchEnd.WinnerPlayerId.Should().Be(2);

    MatchResultReader.TryRead(ref frame, out var result).Should().BeTrue();
    result.EndTick.Should().Be(7);
    result.WinnerTeamId.Should().Be(2);
    result.WinnerPlayerId.Should().Be(2);
    result.Reason.Should().Be(MatchEndReason.Crystal);
    result.IsDraw.Should().BeFalse();
  }

  [Fact]
  public void Update_RecordsTheWinningTeamEvenWhenNoHeroIsLeftToNameAPlayer() {
    var harness = SimHarness.CreateInitialized();
    harness.CompleteMatchSetup();
    var frame = harness.Frame;
    frame.Get<Health>(GetCrystalForTeam(ref frame, teamId: 1)).Current = 0;
    frame.DestroyEntity(harness.FindHero(playerId: 2));

    frame.Tick = 7;
    var collector = new EventCollector();
    collector.BeginTick(7);
    frame.EventRaiser = collector;

    new DeathSystem().Update(ref frame);
    new ScoreSystem().Update(ref frame);

    // The team still won; only the player id the engine wants is unresolvable.
    ref readonly var outcome = ref frame.GetReadOnlySingleton<MatchOutcome>();
    outcome.WinnerTeamId.Should().Be(2);
    outcome.Reason.Should().Be((int)MatchEndReason.Crystal);
    frame.GetReadOnlySingleton<MatchEndStateComponent>().WinnerPlayerId.Should().Be(-1);

    MatchResultReader.TryRead(ref frame, out var result).Should().BeTrue();
    result.WinnerTeamId.Should().Be(2);
    result.WinnerPlayerId.Should().Be(MatchResult.NoWinnerPlayerId);
    result.HasWinner.Should().BeTrue();
    result.Reason.Should().Be(MatchEndReason.Crystal); // not inferred back into Timeout
  }

  [Fact]
  public void Update_AfterCrystalDestructionDoesNotEndMatchWhenMultipleCrystalTeamsRemain() {
    var harness = SimHarness.CreateInitialized(maxPlayers: 3);
    harness.CompleteMatchSetup();
    var frame = harness.Frame;
    EntityRef crystal = GetCrystalForTeam(ref frame, teamId: 1);
    frame.Get<Health>(crystal).Current = 0;

    frame.Tick = 7;
    var collector = new EventCollector();
    collector.BeginTick(7);
    frame.EventRaiser = collector;

    new DeathSystem().Update(ref frame);
    var system = new ScoreSystem();
    system.Update(ref frame);

    collector.Count.Should().Be(1);
    collector.Collected[0].Should().BeOfType<CrystalDestroyedEvent>();

    frame.TryGetSingleton<MatchOutcome>(out _).Should().BeFalse();
    frame.GetReadOnlySingleton<MatchEndStateComponent>().Ended.Should().BeFalse();
  }

  [Fact]
  public void Update_WithASingleContenderNeverDeclaresACrystalWin() {
    // One base on the board is the whole match, not a victory - the last crystal standing has always
    // been standing.
    var harness = SimHarness.CreateInitialized(maxPlayers: 1);
    harness.CompleteMatchSetup();
    var frame = harness.Frame;

    frame.Tick = 7;
    var collector = new EventCollector();
    collector.BeginTick(7);
    frame.EventRaiser = collector;

    new ScoreSystem().Update(ref frame);

    collector.Count.Should().Be(0);
    frame.TryGetSingleton<MatchOutcome>(out _).Should().BeFalse();
    frame.GetReadOnlySingleton<MatchSetupState>().ContenderTeamCount.Should().Be(1);
  }

  [Fact]
  public void Update_DoesNotRaiseGameOverMoreThanOnce() {
    var harness = SimHarness.CreateInitialized();
    harness.CompleteMatchSetup();
    var frame = harness.Frame;
    EntityRef crystal = GetCrystalForTeam(ref frame, teamId: 1);
    frame.Get<Health>(crystal).Current = 0;

    frame.Tick = 7;
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
    frame.GetReadOnlySingleton<MatchOutcome>().EndTick.Should().Be(7); // still the tick it actually ended
    frame.GetReadOnlySingleton<MatchEndStateComponent>().WinnerPlayerId.Should().Be(2);
  }

  [Fact]
  public void Update_OnTimeoutRaisesGameOverAndStoresDrawEndState() {
    var harness = SimHarness.CreateInitialized();
    harness.CompleteMatchSetup();
    var frame = harness.Frame;
    frame.Tick = GetTimeoutTick(ref frame);

    var collector = new EventCollector();
    collector.BeginTick(frame.Tick);
    frame.EventRaiser = collector;

    new ScoreSystem().Update(ref frame);

    collector.Count.Should().Be(1);
    var gameOver = collector.Collected[0].Should().BeOfType<GameOverEvent>().Subject;
    gameOver.WinnerTeamId.Should().Be(MatchOutcome.NoWinnerTeamId);
    gameOver.Reason.Should().Be((int)MatchEndReason.Timeout);

    ref readonly var matchEnd = ref frame.GetReadOnlySingleton<MatchEndStateComponent>();
    matchEnd.Ended.Should().BeTrue();
    matchEnd.WinnerPlayerId.Should().Be(-1);

    MatchResultReader.TryRead(ref frame, out var result).Should().BeTrue();
    result.WinnerPlayerId.Should().Be(MatchResult.NoWinnerPlayerId);
    result.WinnerTeamId.Should().Be(MatchResult.NoWinnerTeamId);
    result.Reason.Should().Be(MatchEndReason.Timeout);
    result.IsDraw.Should().BeTrue();
  }

  [Fact]
  public void Update_OnTickPastTimeoutStillEndsMatch() {
    var harness = SimHarness.CreateInitialized();
    harness.CompleteMatchSetup();
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

    MatchResultReader.TryRead(ref frame, out var result).Should().BeTrue();
    result.Reason.Should().Be(MatchEndReason.Timeout);
  }

  [Fact]
  public void Update_WithZeroDeltaTimeFallsBackToDefaultTickLength() {
    var harness = SimHarness.CreateInitialized();
    harness.CompleteMatchSetup();
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

  [Fact]
  public void TryRead_ReturnsAPlayerRowPerHeroTaggedWithTheWinningSide() {
    var harness = SimHarness.CreateInitialized();
    harness.CompleteMatchSetup();
    var frame = harness.Frame;
    frame.Get<Player>(harness.FindHero(playerId: 1)).HeroKills = 4;
    frame.Get<Player>(harness.FindHero(playerId: 2)).DamageDealt = 250;
    frame.Get<Health>(GetCrystalForTeam(ref frame, teamId: 1)).Current = 0;

    frame.Tick = 7;
    var collector = new EventCollector();
    collector.BeginTick(7);
    frame.EventRaiser = collector;
    new DeathSystem().Update(ref frame);
    new ScoreSystem().Update(ref frame);

    MatchResultReader.TryRead(ref frame, out var result).Should().BeTrue();
    result.Players.Should().HaveCount(2);
    result.Players[0].PlayerId.Should().Be(1);
    result.Players[0].TeamId.Should().Be(1);
    result.Players[0].HeroKills.Should().Be(4);
    result.Players[0].IsWinner.Should().BeFalse();
    result.Players[1].PlayerId.Should().Be(2);
    result.Players[1].DamageDealt.Should().Be(250);
    result.Players[1].IsWinner.Should().BeTrue();
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
