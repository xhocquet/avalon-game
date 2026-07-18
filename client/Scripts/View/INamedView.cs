namespace Meesles.Avalon;

// A view that can report a human-readable name for the selection/focus UI. Implemented by any
// entity or prop that should be click-selectable for inspection (turrets, crystals, shops, the
// fountain, pickups). InputCapture treats any INamedView as a valid single-click target and shows
// DisplayName in the GameUI portrait label; this is independent of the team/controllable checks
// that gate command (move/attack) selection.
public interface INamedView {
  string DisplayName { get; }
}
