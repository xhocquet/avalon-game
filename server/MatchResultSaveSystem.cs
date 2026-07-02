using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Meesles.Avalon.Sim;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Logging;

namespace Meesles.Avalon.Server {
  public class MatchResultSaveSystem : ISystem {
    private readonly IKLogger _logger;
    private readonly string _resultsDirectory;
    private bool _saved;

    public MatchResultSaveSystem(IKLogger logger, string resultsDirectory = null) {
      _logger = logger;
      _resultsDirectory = resultsDirectory ?? Path.Combine(AppContext.BaseDirectory, "Results");
    }

    public void Update(ref Frame frame) {
      if (_saved)
        return;

      if (!MatchResultReader.TryRead(ref frame, frame.Tick, out var result))
        return;

      _saved = true;
      Save(result);
    }

    private void Save(MatchResult result) {
      Directory.CreateDirectory(_resultsDirectory);

      string timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
      string path = Path.Combine(_resultsDirectory, $"match-{timestamp}-tick-{result.EndTick}.json");
      var options = new JsonSerializerOptions {
        WriteIndented = true,
      };
      options.Converters.Add(new JsonStringEnumConverter());

      File.WriteAllText(path, JsonSerializer.Serialize(result, options));
      _logger?.KInformation(
        $"[MatchResult] saved path={path} winnerPlayerId={result.WinnerPlayerId} winnerTeamId={result.WinnerTeamId} reason={result.Reason} endTick={result.EndTick}");
    }
  }
}
