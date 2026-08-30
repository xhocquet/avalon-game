using Godot;
using Meesles.Avalon.Sim;

namespace Meesles.Avalon.Client.Scripts.View;

// Test cheats picked up from the launch args, e.g. `Godot --path client -- --godmode`. Parsed on
// first access rather than in a game node, so the lobby and singleplayer entry points both get them
// without each parsing their own. SimCallbacks turns these into a SetCheatCommand on the first poll.
public static class CheatOptions {
  public static readonly CheatFlags Flags = Parse();

  private static CheatFlags Parse() {
    var flags = CheatFlags.None;
    foreach (var arg in OS.GetCmdlineUserArgs())
      switch (arg) {
        case "--godmode":
          flags |= CheatFlags.GodMode;
          break;
        case "--freeshop":
          flags |= CheatFlags.FreeShop;
          break;
        case "--allcheats":
          flags |= Cheats.All;
          break;
      }

    return flags;
  }
}
