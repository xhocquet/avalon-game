using System;
using Godot;

namespace Meesles.Avalon;

public partial class ChampionPreview : Control {
  [Export] public PackedScene PreviewScene { get; set; }
  [Export] public string InitialAnimation { get; set; } = "";
  [Export] public bool AutoFrame { get; set; }
  [Export] public bool AllowRotation { get; set; }
  [Export(PropertyHint.Range, "0.001,0.03,0.001")] public float RotationSensitivity { get; set; } = 0.01f;
  [Export(PropertyHint.Range, "1,2,0.05")] public float FramingPadding { get; set; } = 1.2f;

  private SubViewport _viewport;
  private Node3D _stage;
  private Node3D _pivot;
  private Node3D _hero;
  private Camera3D _camera;
  private Label _emptyState;
  private AnimationPlayer _animation;
  private bool _dragging;

  public string[] GetAnimationNames() => _animation?.GetAnimationList() ?? Array.Empty<string>();

  public override void _Ready() {
    _viewport = GetNode<SubViewport>("ViewportContainer/Viewport");
    _viewport.OwnWorld3D = true; // Keep the stage in the editor world until runtime.
    _stage = _viewport.GetChildOrNull<Node3D>(0);
    _emptyState = GetNode<Label>("EmptyState");
    if (AllowRotation) {
      MouseFilter = MouseFilterEnum.Stop;
      MouseDefaultCursorShape = CursorShape.Drag;
    }
    Resized += FrameHero;
    VisibilityChanged += UpdateVisibility;
    ShowPreview(PreviewScene, InitialAnimation);
  }

  public override void _GuiInput(InputEvent @event) {
    if (!AllowRotation || _pivot == null) return;
    if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left } button) {
      _dragging = button.Pressed;
      AcceptEvent();
    }
    else if (@event is InputEventMouseMotion motion && _dragging) {
      _dragging = (motion.ButtonMask & MouseButtonMask.Left) != 0;
      if (_dragging) _pivot.RotateY(-motion.Relative.X * RotationSensitivity);
      AcceptEvent();
    }
  }

  public override void _Notification(int what) {
    if (what == NotificationWMWindowFocusOut) _dragging = false;
  }

  public bool ShowFaction(int factionId, string animationName = "") {
    foreach (var def in FactionCatalog.FactionDefs) {
      if (def.Id != factionId) continue;
      ShowPreview(GD.Load<PackedScene>(def.HeroPreviewScenePath), animationName);
      return true;
    }
    Clear();
    return false;
  }

  public void ShowPreview(PackedScene scene, string animationName = "") {
    PreviewScene = scene;
    InitialAnimation = animationName;
    if (_viewport == null) return;
    ClearInstance();
    if (scene != null) {
      _stage = scene.Instantiate<Node3D>();
      _viewport.AddChild(_stage);
      _pivot = _stage.GetNode<Node3D>("ChampionPivot");
      _hero = _pivot.GetChild<Node3D>(0);
      _camera = _stage.GetNode<Camera3D>("Camera3D");
      _camera.Current = true;
      _animation = FindAnimationPlayer(_hero);
      if (_animation != null) {
        foreach (var libraryName in _animation.GetAnimationLibraryList()) {
          var library = (AnimationLibrary)_animation.GetAnimationLibrary(libraryName).Duplicate(true);
          _animation.RemoveAnimationLibrary(libraryName);
          _animation.AddAnimationLibrary(libraryName, library);
        }
        PlayAnimation(string.IsNullOrEmpty(animationName) ? _animation.Autoplay.ToString() : animationName);
      }
      FrameHero();
    }
    UpdateVisibility();
  }

  public bool PlayAnimation(string name, bool loop = true, float speed = 1) {
    if (_animation == null || string.IsNullOrEmpty(name) || !_animation.HasAnimation(name)) return false;
    _animation.GetAnimation(name).LoopMode = loop ? Animation.LoopModeEnum.Linear : Animation.LoopModeEnum.None;
    _animation.SpeedScale = speed;
    _animation.Play(name);
    _animation.Seek(0, true);
    _animation.Advance(0);
    return true;
  }

  public void Clear() {
    PreviewScene = null;
    InitialAnimation = "";
    ClearInstance();
    if (_viewport != null) UpdateVisibility();
  }

  private void ClearInstance() {
    _dragging = false;
    _animation = null;
    _hero = null;
    _pivot = null;
    _camera = null;
    if (_stage == null) return;
    _viewport.RemoveChild(_stage);
    _stage.QueueFree();
    _stage = null;
  }

  private static AnimationPlayer FindAnimationPlayer(Node node) {
    if (node is AnimationPlayer player) return player;
    foreach (var child in node.GetChildren()) {
      var found = FindAnimationPlayer(child);
      if (found != null) return found;
    }
    return null;
  }

  private void UpdateVisibility() {
    var active = _hero != null && IsVisibleInTree();
    if (!active) _dragging = false;
    _viewport.RenderTargetUpdateMode = active ? SubViewport.UpdateMode.Always : SubViewport.UpdateMode.Disabled;
    _viewport.GetParent<Control>().Visible = _hero != null;
    _emptyState.Visible = _hero == null;
    if (_stage != null) _stage.ProcessMode = active ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled;
  }

  public void FrameHero() {
    if (!AutoFrame || _hero == null || Size.X <= 0 || Size.Y <= 0) return;
    Aabb? bounds = null;
    CollectBounds(_hero, ref bounds);
    if (bounds is not { } box) return;
    var aspect = Size.X / Size.Y;
    var height = Mathf.Max(box.Size.Y, box.Size.X / aspect) * FramingPadding;
    _camera.Size = Mathf.Max(height, 0.1f);
    var center = box.GetCenter();
    _camera.Position = center + new Vector3(0, 0, Mathf.Max(box.Size.Length() * 2, 1));
    _camera.LookAt(center);
  }

  private void CollectBounds(Node node, ref Aabb? bounds) {
    if (node is MeshInstance3D { Mesh: not null } mesh) {
      var box = _pivot.GetParent<Node3D>().GlobalTransform.AffineInverse() * mesh.GlobalTransform * mesh.GetAabb();
      bounds = bounds.HasValue ? bounds.Value.Merge(box) : box;
    }
    foreach (var child in node.GetChildren()) CollectBounds(child, ref bounds);
  }
}
