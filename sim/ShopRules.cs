namespace Meesles.Avalon.Sim;

// Shared shop interaction rules. The sim uses this as the authoritative purchase gate (see
// CommandSystem.HandlePurchaseItemCommand); the client uses the same value to decide when to show
// the shop's buy actions, so the UI hint and the authoritative check never disagree. Kept as a
// plain double so both the deterministic sim (FP64.FromDouble) and the Godot view ((float)) read
// one source of truth.
public static class ShopRules {
  // Max planar (XZ) distance in world metres between a hero and its team's Shop marker for a
  // purchase to be allowed.
  public const double InteractRange = 6.0;
}
