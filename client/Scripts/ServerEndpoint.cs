using Godot;

namespace Meesles.Avalon;

// Where the client connects by default. An exported build carries res://server_endpoint.json,
// written from .env at export time, so a distributed client points at the deployed server with
// nothing for the player to type. The file is absent in a working copy, which is what keeps
// `just play` on localhost.
public static class ServerEndpoint {
  public const string DefaultHost = "127.0.0.1";
  public const int DefaultPort = 7777;

  private const string ConfigPath = "res://server_endpoint.json";

  private static bool _resolved;
  private static string _host = DefaultHost;
  private static int _port = DefaultPort;

  public static string Host {
    get {
      Resolve();
      return _host;
    }
  }

  public static int Port {
    get {
      Resolve();
      return _port;
    }
  }

  private static void Resolve() {
    if (_resolved) return;
    _resolved = true;

    if (TryReadConfig(out var host, out var port)) {
      _host = host;
      _port = port;
    }

    // `--server=host:port` wins over the baked file, so one build can be pointed at a scratch
    // server without a re-export.
    foreach (var arg in OS.GetCmdlineUserArgs()) {
      if (!arg.StartsWith("--server=")) continue;
      if (TryParse(arg["--server=".Length..], out host, out port)) {
        _host = host;
        _port = port;
      }
      else {
        GD.PushWarning($"[ServerEndpoint] --server value '{arg}' is not host:port — ignored.");
      }

      break;
    }

    GD.Print($"[ServerEndpoint] {_host}:{_port}");
  }

  private static bool TryReadConfig(out string host, out int port) {
    host = DefaultHost;
    port = DefaultPort;

    if (!FileAccess.FileExists(ConfigPath)) return false;

    using var file = FileAccess.Open(ConfigPath, FileAccess.ModeFlags.Read);
    if (file == null) {
      GD.PushWarning($"[ServerEndpoint] {ConfigPath} unreadable: {FileAccess.GetOpenError()}");
      return false;
    }

    var json = new Json();
    if (json.Parse(file.GetAsText()) != Error.Ok) {
      GD.PushWarning($"[ServerEndpoint] {ConfigPath} is not valid JSON: {json.GetErrorMessage()}");
      return false;
    }

    if (json.Data.VariantType != Variant.Type.Dictionary) return false;

    var data = json.Data.AsGodotDictionary();
    if (data.TryGetValue("host", out var hostValue)) {
      var text = hostValue.AsString().Trim();
      if (text.Length > 0) host = text;
    }

    if (data.TryGetValue("port", out var portValue)) {
      var parsed = portValue.AsInt32();
      if (parsed is > 0 and <= 65535) port = parsed;
    }

    return true;
  }

  private static bool TryParse(string value, out string host, out int port) {
    host = DefaultHost;
    port = DefaultPort;

    var separator = value.LastIndexOf(':');
    if (separator < 1) return false;

    host = value[..separator].Trim();
    return host.Length > 0 && int.TryParse(value[(separator + 1)..], out port) && port is > 0 and <= 65535;
  }
}
