using System.Threading.Tasks;
using Godot;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Godot;

namespace Meesles.Avalon.Client.Scripts.View;

public partial class VisionRenderer : MeshInstance3D {
  private const int TextureSize = 128;
  private readonly byte[] _pixels = new byte[TextureSize * TextureSize];
  private readonly VisionRaster _raster = new();
  private Image _image;
  private ImageTexture _texture;
  private VisionTextureFilter _filter;
  private ShaderMaterial _material;
  private ulong _nextUpdate;
  private int _teamId;
  private Task _renderTask;

  public override void _Ready() {
    _image = Image.CreateFromData(TextureSize, TextureSize, false, Image.Format.R8, _pixels);
    _texture = ImageTexture.CreateFromImage(_image);
    _material = new ShaderMaterial { Shader = GD.Load<Shader>("res://Scripts/View/Vision.gdshader") };
    _filter = new VisionTextureFilter { Name = "VisionTextureFilter" };
    AddChild(_filter);
    _material.SetShaderParameter("vision_texture", _filter.Initialize(_texture));
    _material.RenderPriority = 120;
    Mesh = new QuadMesh { Size = new Vector2(2, 2) };
    MaterialOverride = _material;
    CastShadow = ShadowCastingSetting.Off;
    IgnoreOcclusionCulling = true;
    CustomAabb = new Aabb(new Vector3(-10000, -10000, -10000), new Vector3(20000, 20000, 20000));
    Layers = 1; // Both world and minimap cameras render this pass.
    Visible = false;
  }

  public void Sync(Frame frame, int? playerId) {
    if (!playerId.HasValue || !UnitLookup.TryGetPlayerTeamId(ref frame, playerId.Value, out var teamId) ||
        !frame.AssetRegistry.TryGet<MapLayoutAsset>(out var map) || map.VisionSize.x.RawValue <= 0 ||
        map.VisionSize.y.RawValue <= 0) {
      Visible = false;
      return;
    }
    var now = Time.GetTicksMsec();
    var noFog = CheatsController.IsFogDisabled(ref frame, teamId);
    foreach (var node in GetTree().GetNodesInGroup("units")) {
      if (node is not EntityViewNode view || !view.EntityRef.IsValid ||
          !frame.Has<UnitIdentity>(view.EntityRef))
        continue;
      view.Visible = noFog || (frame.Has<UnitVisibility>(view.EntityRef)
        ? (frame.GetReadOnly<UnitVisibility>(view.EntityRef).Teams & Visibility.TeamBit(teamId)) != 0
        : Visibility.IsUnitVisible(ref frame, teamId, view.EntityRef));
    }
    if (noFog) {
      Visible = false;
      return;
    }
    if (_renderTask != null) {
      if (!_renderTask.IsCompleted)
        return;
      _renderTask.GetAwaiter().GetResult();
      _renderTask = null;
      if (teamId == _teamId && ReferenceEquals(map, _raster.Map)) {
        _image.SetData(TextureSize, TextureSize, false, Image.Format.R8, _pixels);
        _texture.Update(_image);
        _filter.Refresh();
        _material.SetShaderParameter("vision_origin", new Vector2(map.VisionOrigin.x.ToFloat(), map.VisionOrigin.y.ToFloat()));
        _material.SetShaderParameter("vision_size", new Vector2(map.VisionSize.x.ToFloat(), map.VisionSize.y.ToFloat()));
        Visible = true;
      }
    }
    if (teamId == _teamId && ReferenceEquals(map, _raster.Map) && now < _nextUpdate)
      return;
    if (teamId != _teamId || !ReferenceEquals(map, _raster.Map))
      Visible = false;
    _teamId = teamId;
    _nextUpdate = now + 100;
    _raster.Capture(ref frame, teamId, map);
    _renderTask = Task.Run(RenderRaster);
  }

  private void RenderRaster() => _raster.Render(TextureSize, TextureSize, _pixels);

  public override void _ExitTree() {
    _texture?.Dispose();
    _image?.Dispose();
    _material?.Dispose();
  }
}
