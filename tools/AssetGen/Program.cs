// Headless DataAsset compiler: JSON -> .bytes, no Godot editor required.
// Usage: dotnet run --project tools/AssetGen -- <input.json|inputDir> <output.bytes>
// Default (no args): client/Sim/Data/Assets/ -> client/Sim/Data/Assets.bytes  (data stays in client/ for Godot res://)
//
// A directory input is walked recursively and every *.json merged into one collection, so adding an
// asset means dropping a file in, with no index to maintain. Files are ordered by relative path so
// the output bytes stay stable across machines.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using xpTURN.Klotho.ECS;        // DataAssetWriter
using xpTURN.Klotho.ECS.Json;   // DataAssetJsonConverter

string inPath, outPath;
if (args.Length >= 2) {
  inPath = args[0];
  outPath = args[1];
}
else {
  string repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
  inPath = Path.Combine(repoRoot, "client", "Sim", "Data", "Assets");
  outPath = Path.Combine(repoRoot, "client", "Sim", "Data", "Assets.bytes");
}

bool isDir = Directory.Exists(inPath);
if (!isDir && !File.Exists(inPath)) {
  Console.Error.WriteLine($"Input not found: {inPath}");
  return 1;
}

string json;
int fileCount = 1;
if (isDir) {
  string[] files = Directory.GetFiles(inPath, "*.json", SearchOption.AllDirectories)
    .OrderBy(p => Path.GetRelativePath(inPath, p).Replace('\\', '/'), StringComparer.Ordinal)
    .ToArray();
  if (files.Length == 0) {
    Console.Error.WriteLine($"No .json files under: {inPath}");
    return 1;
  }
  fileCount = files.Length;

  var rows = new List<string>();
  var owners = new Dictionary<int, string>(); // AssetId -> file that declared it
  foreach (string file in files) {
    string rel = Path.GetRelativePath(inPath, file).Replace('\\', '/');
    using var doc = JsonDocument.Parse(File.ReadAllText(file), new JsonDocumentOptions {
      CommentHandling = JsonCommentHandling.Skip,
      AllowTrailingCommas = true,
    });
    if (doc.RootElement.ValueKind != JsonValueKind.Array) {
      Console.Error.WriteLine($"{rel}: expected a top-level array of assets.");
      return 1;
    }
    foreach (JsonElement row in doc.RootElement.EnumerateArray()) {
      if (!row.TryGetProperty("AssetId", out JsonElement idElem) || !idElem.TryGetInt32(out int assetId)) {
        Console.Error.WriteLine($"{rel}: an asset row is missing an integer AssetId.");
        return 1;
      }
      if (owners.TryGetValue(assetId, out string owner)) {
        Console.Error.WriteLine($"Duplicate AssetId {assetId} in {rel}, already declared in {owner}.");
        return 1;
      }
      owners[assetId] = rel;
      rows.Add(row.GetRawText());
    }
  }

  var sb = new StringBuilder("[\n");
  sb.AppendJoin(",\n", rows);
  sb.Append("\n]");
  json = sb.ToString();
}
else {
  json = File.ReadAllText(inPath);
}

byte[] bytes = DataAssetJsonConverter.ConvertMixedJsonToBytes(json);
DataAssetWriter.SaveToFile(outPath, bytes);
Console.WriteLine($"[AssetGen] {inPath} ({fileCount} file(s)) -> {outPath} ({bytes.Length} bytes)");
return 0;
