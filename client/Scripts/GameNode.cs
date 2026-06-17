using global::Godot;

namespace Meesles.Avalon {
	public abstract partial class GameNode : Node {
		protected InputCapture Input;
		protected Menu Menu;
		protected Hud Hud;

		protected void InitializeSharedNodes() {
			Input = new InputCapture();
			Menu = GetNode<Menu>("UILayer/Menu");
			Hud = GetNode<Hud>("UILayer/Hud");
		}

		protected void SetupView3D() {
			var cam = GetNodeOrNull<Camera3D>("Camera3D");
			if (cam != null) {
				cam.Environment = new global::Godot.Environment {
					BackgroundMode = global::Godot.Environment.BGMode.Color,
					BackgroundColor = new Color(0.12f, 0.13f, 0.18f),
					AmbientLightSource = global::Godot.Environment.AmbientSource.Color,
					AmbientLightColor = new Color(0.5f, 0.5f, 0.5f),
					AmbientLightEnergy = 1.0f,
				};
			}

			var light = GetNodeOrNull<DirectionalLight3D>("DirectionalLight3D");
			light?.LookAtFromPosition(new Vector3(4, 10, 4), Vector3.Zero, Vector3.Up);
		}

		public override void _ExitTree() {
			Input?.Dispose();
		}
	}
}
