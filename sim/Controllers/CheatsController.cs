using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

// Stores per-player development cheat flags in CheatState
public static class CheatsController {
  public const CheatFlags All = CheatFlags.GodMode | CheatFlags.FreeShop;

  public static void Set(ref Frame frame, int playerId, CheatFlags flags, bool enabled) {
    var current = (CheatFlags)GetFlags(ref frame, playerId);
    var updated = enabled ? current | flags : current & ~flags;
    if (updated == current)
      return;

    if (!frame.TryGetSingleton<CheatState>(out _)) {
      var entity = frame.CreateEntity();
      var state = new CheatState();
      state.SetFlags(playerId, (int)updated);
      frame.Add(entity, state);
    }
    else if (!frame.GetSingleton<CheatState>().SetFlags(playerId, (int)updated)) {
      SimLog.Warning(ref frame,
        $"[Cheats] event=rejected tick={frame.Tick} playerId={playerId} flags={updated} reason=table_full");
      return;
    }

    SimLog.Info(ref frame, $"[Cheats] event=set tick={frame.Tick} playerId={playerId} flags={updated}");
  }

  public static bool IsEnabled(ref Frame frame, int playerId, CheatFlags flag) {
    return ((CheatFlags)GetFlags(ref frame, playerId) & flag) != 0;
  }

  // Requires every requested flag
  public static bool AreAllEnabled(ref Frame frame, int playerId, CheatFlags flags) {
    return ((CheatFlags)GetFlags(ref frame, playerId) & flags) == flags;
  }

  // GodMode check for damage
  public static bool BlocksDamage(ref Frame frame, EntityRef target) {
    if (!frame.Has<Hero>(target))
      return false;

    return IsEnabled(ref frame, frame.GetReadOnly<Hero>(target).PlayerId, CheatFlags.GodMode);
  }

  private static int GetFlags(ref Frame frame, int playerId) {
    return frame.TryGetSingleton<CheatState>(out _)
      ? frame.GetReadOnlySingleton<CheatState>().GetFlags(playerId)
      : 0;
  }
}
