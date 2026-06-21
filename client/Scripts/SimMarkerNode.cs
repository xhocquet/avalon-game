using global::Godot;

namespace Meesles.Avalon {
  [Tool]
  [GlobalClass]
  public partial class SimMarkerNode : Node3D {
    [Export] public MapMarkerType MarkerType { get; set; }
    [Export] public int Team { get; set; }
  }
}
