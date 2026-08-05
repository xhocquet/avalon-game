using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Logging;

namespace Meesles.Avalon.Sim.Commands;

// The single gate every command passes through before a handler runs. Two things are checked here and
// nowhere else:
//
//   structural — the payload survived deserialization intact (UnitIdList.IsValid). A hostile or
//                corrupt frame is dropped whole rather than executed against a partial selection.
//   domain     — each field names something that can exist: a coordinate inside the world envelope,
//                an asset id the registry knows. Handlers can then trust their inputs and spend their
//                checks on game state (ownership, gold, range), which is where those belong.
//
// This runs inside the simulation, so client prediction and the authoritative server reach the same
// verdict for the same frame. Rejecting on the server ingest path instead would accept a command
// locally that the server threw away, and the client would mispredict every time.
public static class CommandValidation {
  public static bool Accept(ref Frame frame, ICommand command) {
    switch (command) {
      case MoveCommand move:
        return AcceptSelection(ref frame, move, move.UnitIds) && AcceptMoveTarget(ref frame, move);
      case AttackCommand attack:
        return AcceptSelection(ref frame, attack, attack.UnitIds);
      case SelectFactionCommand faction:
        return AcceptFaction(ref frame, faction);
      case UpgradeSkillCommand upgrade:
        return AcceptSkillSlot(ref frame, upgrade, upgrade.Slot);
      case CastSkillCommand cast:
        return AcceptSkillSlot(ref frame, cast, cast.Slot);
      default:
        return true;
    }
  }

  private static bool AcceptSelection(ref Frame frame, ICommand command, UnitIdList unitIds) {
    if (unitIds.IsValid)
      return true;

    Reject(ref frame, command, "malformed_selection");
    return false;
  }

  private static bool AcceptMoveTarget(ref Frame frame, MoveCommand command) {
    if (IsInWorldEnvelope(command.TargetX) && IsInWorldEnvelope(command.TargetZ))
      return true;

    Reject(ref frame, command, $"target_out_of_bounds x={command.TargetX} z={command.TargetZ}");
    return false;
  }

  private static bool AcceptFaction(ref Frame frame, SelectFactionCommand command) {
    if (frame.AssetRegistry.TryGet<FactionAsset>(command.FactionId, out _))
      return true;

    Reject(ref frame, command, $"faction_asset_missing factionId={command.FactionId}");
    return false;
  }

  // The only gate between a wire slot index and the fixed buffers on SkillsComponent, which are
  // indexed unchecked.
  private static bool AcceptSkillSlot(ref Frame frame, ICommand command, int slot) {
    if (SkillsComponent.IsValidSlot(slot))
      return true;

    Reject(ref frame, command, $"skill_slot_out_of_range slot={slot}");
    return false;
  }

  private static bool IsInWorldEnvelope(FP64 coordinate) {
    var limit = FP64.FromInt(CommandLimits.MaxWorldCoordinate);
    return coordinate >= -limit && coordinate <= limit;
  }

  private static void Reject(ref Frame frame, ICommand command, string reason) {
    frame.Logger.KWarning(
      $"[CommandValidation] REJECT tick={frame.Tick} playerId={command.PlayerId} cmd={command.GetType().Name} reason={reason}");
  }
}
