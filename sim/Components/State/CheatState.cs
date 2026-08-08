using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// Singleton table of per-player test cheats, written by SetCheatCommand. Keyed by player rather than
// stamped on the hero because the flag can land before HeroSpawnSystem has spawned one, and has to
// outlive the respawn that resets hero state.
// Size: 2 * MaxEntries * 4 = 64B, inside the 128-byte component ceiling.
[KlothoComponent(ComponentIds.CheatState)]
[KlothoSingletonComponent]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public unsafe partial struct CheatState : IComponent {
  public const int MaxEntries = 8;

  public fixed int PlayerIds[MaxEntries]; // 0 = free slot
  public fixed int Flags[MaxEntries];

  public readonly int GetFlags(int playerId) {
    for (var i = 0; i < MaxEntries; i++)
      if (PlayerIds[i] == playerId)
        return Flags[i];

    return 0;
  }

  // False when every slot is taken by another player, which drops the cheat rather than evicting one.
  public bool SetFlags(int playerId, int flags) {
    var free = -1;
    for (var i = 0; i < MaxEntries; i++) {
      if (PlayerIds[i] == playerId) {
        Flags[i] = flags;
        return true;
      }

      if (free < 0 && PlayerIds[i] == 0)
        free = i;
    }

    if (free < 0)
      return false;

    PlayerIds[free] = playerId;
    Flags[free] = flags;
    return true;
  }
}
