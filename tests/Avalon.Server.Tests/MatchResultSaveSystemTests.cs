using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using FluentAssertions;
using Meesles.Avalon.Sim;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Logging;
using xpTURN.Klotho.Network;
using Xunit;

namespace Meesles.Avalon.Server.Tests;

public sealed class MatchResultSaveSystemTests : IDisposable {
  private readonly string _resultsDirectory = Path.Combine(Path.GetTempPath(), $"avalon-server-tests-{Guid.NewGuid():N}");

  [Fact]
  public void Save_WritesTheMatchAndServerSessionMetadata() {
    var logger = new TestLogger();
    var system = new MatchResultSaveSystem(logger, resultsDirectory: _resultsDirectory);
    system.SetSessionParameters(randomSeed: 1234, maxPlayers: 4, minPlayers: 2);

    var path = system.Save(CreateResult());

    File.Exists(path).Should().BeTrue();
    path.Should().MatchRegex(@"match-\d{8}-\d{6}-tick-42\.json$");
    using var document = JsonDocument.Parse(File.ReadAllText(path));
    var root = document.RootElement;
    root.GetProperty("Session").GetProperty("RandomSeed").GetInt32().Should().Be(1234);
    root.GetProperty("Session").GetProperty("MaxPlayers").GetInt32().Should().Be(4);
    root.GetProperty("Session").GetProperty("MinPlayers").GetInt32().Should().Be(2);
    root.GetProperty("Session").GetProperty("SavedAtUtc").GetString().Should().EndWith("Z");
    root.GetProperty("Match").GetProperty("EndTick").GetInt32().Should().Be(42);
    root.GetProperty("Match").GetProperty("Reason").GetString().Should().Be("Crystal");
    root.GetProperty("Match").GetProperty("Players")[0].GetProperty("Name").GetString().Should().Be("Ari");
    logger.Messages.Should().ContainSingle().Which.Should().Contain("winnerTeamId=2");
  }

  [Fact]
  public void ResolveName_UsesTheMatchingNonEmptyRosterDisplayName() {
    IReadOnlyList<IPlayerInfo> roster = [
      new PlayerInfo(1, "", "account-1"),
      new PlayerInfo(2, "Ari", "account-2")
    ];

    MatchResultSaveSystem.ResolveName(roster, 2).Should().Be("Ari");
    MatchResultSaveSystem.ResolveName(roster, 1).Should().BeNull();
    MatchResultSaveSystem.ResolveName(roster, 3).Should().BeNull();
    MatchResultSaveSystem.ResolveName(null, 2).Should().BeNull();
  }

  [Fact]
  public void Update_DoesNotCreateARecordBeforeTheMatchHasEnded() {
    var system = new MatchResultSaveSystem(new TestLogger(), resultsDirectory: _resultsDirectory);
    var simulation = new EcsSimulation(maxEntities: 8, maxRollbackTicks: 1, deltaTimeMs: 16);
    simulation.Initialize();
    var frame = simulation.Frame;

    system.Update(ref frame);

    Directory.Exists(_resultsDirectory).Should().BeFalse();
  }

  public void Dispose() {
    if (Directory.Exists(_resultsDirectory))
      Directory.Delete(_resultsDirectory, recursive: true);
  }

  private static MatchResult CreateResult() {
    return new MatchResult {
      EndTick = 42,
      DurationMs = 672,
      WinnerPlayerId = 2,
      WinnerTeamId = 2,
      Reason = MatchEndReason.Crystal,
      Context = new MatchContext { MapName = "TestMap" },
      Players = [new PlayerResult { PlayerId = 2, Name = "Ari", TeamId = 2, IsWinner = true }]
    };
  }

  private sealed class TestLogger : IKLogger {
    public List<string> Messages { get; } = [];

    public bool IsEnabled(KLogLevel level) => true;

    public void Log(KLogLevel level, string message, Exception exception) {
      Messages.Add(message);
    }
  }

  private sealed class PlayerInfo(int playerId, string displayName, string account) : IPlayerInfo {
    public int PlayerId { get; } = playerId;
    public string DisplayName { get; } = displayName;
    public string Account { get; } = account;
    public bool IsReady => true;
    public int Ping => 0;
    public PlayerConnectionState ConnectionState => PlayerConnectionState.Connected;
  }
}
