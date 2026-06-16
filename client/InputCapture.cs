// Godot input capture for Avalon: WASD and arrow keys -> FP64 H/V in [-1,1].
using System;
using global::Godot;
using xpTURN.Klotho.Deterministic.Math;

namespace Meesles.Avalon
{
  public class InputCapture : IDisposable
  {
    public FP64 Horizontal { get; private set; }
    public FP64 Vertical { get; private set; }

    public void CaptureInput()
    {
      float h = Input.GetActionStrength("ui_right") - Input.GetActionStrength("ui_left");
      float v = Input.GetActionStrength("ui_down") - Input.GetActionStrength("ui_up");
      Horizontal = FP64.FromFloat(h);
      Vertical = FP64.FromFloat(v);
    }

    public void Dispose()
    {
    }
  }
}
