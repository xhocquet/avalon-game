using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using CsvHelper;
using FluentAssertions;
using Meesles.Avalon;
using Xunit;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;

namespace Meesles.Avalon.Sim.Tests;

public class DeterminismBaselineTests {
  private const int Seed = 8675309;
  private const int TickCount = 360;
  private const string CommandGeneratorName = "TwoPlayerPatternedMoveCommands";

  [Fact]
  public void SameInputSequence_ProducesSameHashSequence() {
    var simA = SimHarness.CreateInitialized();
    var simB = SimHarness.CreateInitialized();
    var hashesA = new List<HashSample>(TickCount);
    var hashesB = new List<HashSample>(TickCount);

    for (int tick = 0; tick < TickCount; tick++) {
      ICommand[] commands = CreateCommands(tick);

      simA.Tick(commands);
      simB.Tick(CreateCommands(tick));

      hashesA.Add(new HashSample(simA.Frame.Tick, simA.StateHash));
      hashesB.Add(new HashSample(simB.Frame.Tick, simB.StateHash));
    }

    hashesA.Should().Equal(hashesB);
  }

  [Fact]
  public void HashDump_CanBeWrittenAsCsv() {
    IReadOnlyList<HashSample> hashes = RunHashDump(TickCount);
    string path = Path.Combine(ArtifactDirectory(), "hashes_seed8675309.csv");

    using (var writer = new StreamWriter(path))
    using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture)) {
      csv.WriteRecords(hashes);
    }

    File.Exists(path).Should().BeTrue();
    File.ReadLines(path).Should().HaveCount(TickCount + 1);
  }

  [Fact]
  public void HashDump_CanBeWrittenAsJson() {
    IReadOnlyList<HashSample> hashes = RunHashDump(TickCount);
    var dump = new HashRunDump(
        TickCount,
        SimHarness.DefaultMaxPlayers,
        Seed,
        CommandGeneratorName,
        hashes[^1].Hash,
        hashes);
    string path = Path.Combine(ArtifactDirectory(), "hashes_seed8675309.json");

    var options = new JsonSerializerOptions { WriteIndented = true };
    File.WriteAllText(path, JsonSerializer.Serialize(dump, options));

    File.Exists(path).Should().BeTrue();

    var roundTrip = JsonSerializer.Deserialize<HashRunDump>(File.ReadAllText(path), options);
    roundTrip.Should().NotBeNull();
    roundTrip!.TickCount.Should().Be(TickCount);
    roundTrip.FinalHash.Should().Be(hashes[^1].Hash);
    roundTrip.Hashes.Should().HaveCount(TickCount);
  }

  private static IReadOnlyList<HashSample> RunHashDump(int tickCount) {
    var harness = SimHarness.CreateInitialized();
    var hashes = new List<HashSample>(tickCount);

    for (int tick = 0; tick < tickCount; tick++) {
      harness.Tick(CreateCommands(tick));
      hashes.Add(new HashSample(harness.Frame.Tick, harness.StateHash));
    }

    return hashes;
  }

  private static ICommand[] CreateCommands(int tick) {
    return [
        CreateCommand(playerId: 1, tick, phase: 0),
        CreateCommand(playerId: 2, tick, phase: 3),
    ];
  }

  private static MoveCommand CreateCommand(int playerId, int tick, int phase) {
    FP64 h = PatternValue(tick + phase);
    FP64 v = PatternValue(tick + phase + 1);
    return SimHarness.MoveCommand(playerId, tick, h, v);
  }

  private static FP64 PatternValue(int value) {
    return Math.Abs(value) % 4 switch {
        0 => FP64.Zero,
        1 => FP64.One,
        2 => FP64.Zero,
        _ => -FP64.One,
    };
  }

  private static string ArtifactDirectory() {
    string root = FindRepositoryRoot();
    string path = Path.Combine(root, "TestResults", "avalon-sim");
    Directory.CreateDirectory(path);
    return path;
  }

  private static string FindRepositoryRoot() {
    var directory = new DirectoryInfo(AppContext.BaseDirectory);

    while (directory != null) {
      if (File.Exists(Path.Combine(directory.FullName, "TEST_HARNESS_PLAN.md")))
        return directory.FullName;

      directory = directory.Parent;
    }

    return AppContext.BaseDirectory;
  }

  public sealed record HashSample(int Tick, long Hash);

  public sealed record HashRunDump(
      int TickCount,
      int MaxPlayers,
      int Seed,
      string CommandGeneratorName,
      long FinalHash,
      IReadOnlyList<HashSample> Hashes);
}
