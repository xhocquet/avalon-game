using Godot;
using Meesles.Avalon.Sim;

namespace Meesles.Avalon.Client.Scripts.View;

// Launch cheats shared by the lobby and singleplayer entry points.
public static class CheatOptions {
  public static readonly CheatFlags Flags = Parse();
  public static readonly bool AllCheats = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--allcheats") >= 0;

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
          flags |= CheatsController.All & ~CheatFlags.NoFog;
          break;
        case "--nofog":
          flags |= CheatFlags.NoFog;
          break;
      }

    return flags;
  }
}
