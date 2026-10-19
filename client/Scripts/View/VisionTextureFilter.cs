using Godot;

namespace Meesles.Avalon.Client.Scripts.View;

public partial class VisionTextureFilter : Node {
  private const int Size = 512;
  private SubViewport _upscale;
  private SubViewport _horizontal;
  private SubViewport _vertical;

  public Texture2D Initialize(Texture2D source) {
    _upscale = CreatePass("Reconstruct", "res://Scripts/View/VisionUpscale.gdshader", source);
    _horizontal = CreatePass("BlurHorizontal", "res://Scripts/View/VisionBlur.gdshader", _upscale.GetTexture(), Vector2.Right);
    _vertical = CreatePass("BlurVertical", "res://Scripts/View/VisionBlur.gdshader", _horizontal.GetTexture(), Vector2.Down);
    return _vertical.GetTexture();
  }

  public void Refresh() {
    _upscale.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
    _horizontal.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
    _vertical.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
  }

  private SubViewport CreatePass(string name, string shaderPath, Texture2D source, Vector2? direction = null) {
    var viewport = new SubViewport {
      Name = name,
      Size = new Vector2I(Size, Size),
      Disable3D = true,
      GuiDisableInput = true,
      RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled
    };
    AddChild(viewport);
    var material = new ShaderMaterial { Shader = GD.Load<Shader>(shaderPath) };
    material.SetShaderParameter("source_mask", source);
    if (direction.HasValue)
      material.SetShaderParameter("direction", direction.Value);
    viewport.AddChild(new ColorRect {
      Size = new Vector2(Size, Size),
      MouseFilter = Control.MouseFilterEnum.Ignore,
      Material = material
    });
    return viewport;
  }
}
