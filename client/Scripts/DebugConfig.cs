namespace Meesles.Avalon;

// Single home for temporary debug visualisation toggles. Everything here defaults OFF — flip a flag
// on locally while investigating, but don't commit it enabled.
public static class DebugConfig {
  // Draw a translucent capsule (and log its derived size) for every click-selection pick collider
  // added by EntityViewPhysics.AddSelectionCollider. Handy when tuning selection hitboxes; these are
  // the same colliders InputCapture raycasts against.
  public static bool DrawSelectionColliders = false;
}
