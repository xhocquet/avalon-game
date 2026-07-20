using xpTURN.Klotho.Core;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim;

// Raised by TeamPruneSystem for each team removed at match setup because no player is on it. The
// client frees that team's authored base props (World.tscn Team{TeamId}) so the view matches the
// simulation. Synced so view listeners react only to the authoritative prune, never a mispredicted
// one — freeing Godot nodes can't be undone.
[KlothoSerializable(112)]
public partial class TeamPrunedEvent : SimulationEvent {
  [KlothoOrder(0)] public int TeamId;
  public override EventMode Mode => EventMode.Synced;
}
