using System;
using Godot;

namespace Meesles.Avalon.Client.Scripts.View;

// `--quickplay` auto-joins and auto-readies the lobby so scripts/quickplay.ps1 lands straight in a
// match. It is a one-shot: returning to the lobby after a match re-runs LobbyGameNode._Ready and the
// command line still carries the flag, so without the latch every match after the first would start
// itself the moment the player got back. Static, which is what makes it outlive the scene swap.
public static class QuickplayLaunch {
  private static bool _pending = Array.IndexOf(OS.GetCmdlineUserArgs(), "--quickplay") >= 0;

  public static bool Consume() {
    if (!_pending)
      return false;

    _pending = false;
    return true;
  }
}
