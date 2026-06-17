using global::Godot;

namespace Meesles.Avalon
{
  public partial class CameraController : Camera3D
  {
    [Export] public float Height = 14f;
    [Export] public float PanSpeed = 10f;

    private Vector3 _focus = Vector3.Zero;

    public override void _Ready()
    {
      ApplyView();
    }

    public override void _Process(double delta)
    {
      float x = Input.GetActionStrength("camera_right") - Input.GetActionStrength("camera_left");
      float z = Input.GetActionStrength("camera_down") - Input.GetActionStrength("camera_up");
      if (Mathf.IsZeroApprox(x) && Mathf.IsZeroApprox(z)) return;

      _focus += new Vector3(x, 0f, z) * PanSpeed * (float)delta;
      ApplyView();
    }

    private void ApplyView()
    {
      LookAtFromPosition(_focus + new Vector3(0f, Height, 0f), _focus, new Vector3(0f, 0f, -1f));
    }
  }
}
