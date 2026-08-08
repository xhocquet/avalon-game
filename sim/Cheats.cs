using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

// The rules behind SetCheatCommand: test-only toggles a player turns on for its own hero, launched
// from the client with `--godmode` and friends. Nothing gates these beyond the per-player scope, so
// they are a development aid, not a mode a shipped server should accept.
//
// State lives in the CheatState singleton, which means it snapshots and rolls back like anything else
// and the predicting client and the server reach the same verdict on the same tick.
public static class Cheats {
  public const CheatFlags All = CheatFlags.GodMode;

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
        $"[Cheats] table full, dropping tick={frame.Tick} playerId={playerId} flags={updated}");
      return;
    }

    SimLog.Info(ref frame, $"[Cheats] SET tick={frame.Tick} playerId={playerId} flags={updated}");
  }

  public static bool IsEnabled(ref Frame frame, int playerId, CheatFlags flag) {
    return ((CheatFlags)GetFlags(ref frame, playerId) & flag) != 0;
  }

  // Every bit set, not any - the client resends its launch flags until the sim agrees it has them all.
  public static bool AreAllEnabled(ref Frame frame, int playerId, CheatFlags flags) {
    return ((CheatFlags)GetFlags(ref frame, playerId) & flags) == flags;
  }

  // Asked by DamageApplication for every hit, so it early-outs on the table being absent - the common
  // case is a match where nobody cheated and the singleton was never created.
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
