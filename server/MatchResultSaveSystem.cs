using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Meesles.Avalon.Sim;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Logging;
using xpTURN.Klotho.Network;

namespace Meesles.Avalon.Server {
  // What the sim cannot see: the session parameters it was started with and when the file was cut.
  public readonly struct MatchSession {
    public string SavedAtUtc { get; init; }
    public int RandomSeed { get; init; }
    public int MaxPlayers { get; init; }
    public int MinPlayers { get; init; }
  }

  public readonly struct MatchRecord {
    public MatchSession Session { get; init; }
    public MatchResult Match { get; init; }
  }

  public class MatchResultSaveSystem : ISystem {
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    private readonly IKLogger _logger;
    private readonly string _resultsDirectory;
    private readonly Func<IReadOnlyList<IPlayerInfo>> _rosterProvider;
    private int _randomSeed;
    private int _maxPlayers;
    private int _minPlayers;
    private bool _saved;

    public MatchResultSaveSystem(IKLogger logger, Func<IReadOnlyList<IPlayerInfo>> rosterProvider = null,
      string resultsDirectory = null) {
      _logger = logger;
      _rosterProvider = rosterProvider;
      _resultsDirectory = resultsDirectory ?? Path.Combine(AppContext.BaseDirectory, "Results");
    }

    // The engine only hands these over at world init, well after the system is registered.
    public void SetSessionParameters(int randomSeed, int maxPlayers, int minPlayers) {
      _randomSeed = randomSeed;
      _maxPlayers = maxPlayers;
      _minPlayers = minPlayers;
    }

    public void Update(ref Frame frame) {
      if (_saved)
        return;

      if (!MatchResultReader.TryRead(ref frame, out var result, ResolveName))
        return;

      _saved = true;
      Save(result);
    }

    // Names ride the join handshake and never enter the sim, so they are resolved off the live room
    // roster at the moment the match ends. Null when there is no roster (offline / headless harness).
    private string ResolveName(int playerId) {
      var roster = _rosterProvider?.Invoke();
      if (roster == null)
        return null;

      for (var i = 0; i < roster.Count; i++)
        if (roster[i].PlayerId == playerId)
          return string.IsNullOrEmpty(roster[i].DisplayName) ? null : roster[i].DisplayName;

      return null;
    }

    private void Save(MatchResult result) {
      Directory.CreateDirectory(_resultsDirectory);

      var savedAt = DateTimeOffset.UtcNow;
      string path = Path.Combine(_resultsDirectory,
        $"match-{savedAt:yyyyMMdd-HHmmss}-tick-{result.EndTick}.json");
      var record = new MatchRecord {
        Session = new MatchSession {
          // Literal Z rather than round-trip "O": its "+00:00" offset serializes as +.
          SavedAtUtc = savedAt.ToString("yyyy-MM-ddTHH:mm:ss.fff'Z'"),
          RandomSeed = _randomSeed,
          MaxPlayers = _maxPlayers,
          MinPlayers = _minPlayers
        },
        Match = result
      };

      File.WriteAllText(path, JsonSerializer.Serialize(record, SerializerOptions));
      _logger.KInformation(
        $"[MatchResult] saved path={path} winnerTeamId={result.WinnerTeamId} winnerPlayerId={result.WinnerPlayerId} " +
        $"reason={result.Reason} endTick={result.EndTick} players={result.Players.Length}");
    }

    private static JsonSerializerOptions CreateSerializerOptions() {
      var options = new JsonSerializerOptions { WriteIndented = true };
      options.Converters.Add(new JsonStringEnumConverter());
      return options;
    }
  }
}
