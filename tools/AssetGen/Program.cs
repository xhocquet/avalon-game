// Headless DataAsset compiler: JSON -> .bytes, no Godot editor required.
// Usage: dotnet run --project tools/AssetGen -- <input.json> <output.bytes>
// Default (no args): client/Sim/Data/Assets.json -> client/Sim/Data/Assets.bytes  (data stays in client/ for Godot res://)
using System;
using System.IO;
using xpTURN.Klotho.ECS;        // DataAssetWriter
using xpTURN.Klotho.ECS.Json;   // DataAssetJsonConverter

string inPath, outPath;
if (args.Length >= 2) {
  inPath = args[0];
  outPath = args[1];
}
else {
  string repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
  inPath = Path.Combine(repoRoot, "client", "Sim", "Data", "Assets.json");
  outPath = Path.Combine(repoRoot, "client", "Sim", "Data", "Assets.bytes");
}

if (!File.Exists(inPath)) {
  Console.Error.WriteLine($"Input not found: {inPath}");
  return 1;
}

string json = File.ReadAllText(inPath);
byte[] bytes = DataAssetJsonConverter.ConvertMixedJsonToBytes(json);
DataAssetWriter.SaveToFile(outPath, bytes);
Console.WriteLine($"[AssetGen] {inPath} -> {outPath} ({bytes.Length} bytes)");
return 0;
