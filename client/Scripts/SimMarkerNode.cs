using global::Godot;

namespace Meesles.Avalon {
  [Tool]
  [GlobalClass]
  public partial class SimMarkerNode : Node3D {
	[Export] public int MarkerId { get; set; }
	[Export] public MapMarkerType MarkerType { get; set; }
	[Export] public int Team { get; set; }
  }
}
