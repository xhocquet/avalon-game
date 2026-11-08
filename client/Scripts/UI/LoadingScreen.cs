using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using Meesles.Avalon.Client.Scripts.Entities;
using Meesles.Avalon.Client.Scripts.View;

namespace Meesles.Avalon;

public partial class LoadingScreen : CanvasLayer {
  private static LoadingScreen _active;
  private readonly List<Resource> _resources = new();
  private ProgressBar _bar;
  private Label _percentage;
  private Button _dismiss;

  public override void _Ready() {
    _bar = GetNode<ProgressBar>("%ProgressBar");
    _percentage = GetNode<Label>("%Percentage");
    _dismiss = GetNode<Button>("%Dismiss");
  }

  public static void Begin(Node source, GameTypeCatalog.GameTypeDef gameType, Action beforeSwitch = null,
    Action onFailure = null) {
    if (IsInstanceValid(_active)) return;
    var screen = GD.Load<PackedScene>(Scenes.LoadingScreen).Instantiate<LoadingScreen>();
    _active = screen;
    source.GetTree().Root.AddChild(screen);
    _ = screen.LoadGame(source, gameType, beforeSwitch, onFailure);
  }

  private async Task LoadGame(Node source, GameTypeCatalog.GameTypeDef gameType, Action beforeSwitch,
    Action onFailure) {
    var tree = GetTree();
    var sourceProcessMode = source.ProcessMode;
    source.ProcessMode = ProcessModeEnum.Disabled;
    try {
      await DrawFrame();
      var paths = ResourcePaths(gameType);
      PackedScene target = null;
      for (var i = 0; i < paths.Count; i++) {
        var path = paths[i];
        var error = ResourceLoader.LoadThreadedRequest(path);
        if (error != Error.Ok) throw new InvalidOperationException($"{path}: {error}");
        var progress = new Godot.Collections.Array();
        while (true) {
          var status = ResourceLoader.LoadThreadedGetStatus(path, progress);
          var fraction = progress.Count > 0 ? progress[0].AsDouble() : 0;
          SetProgress(90.0 * (i + fraction) / paths.Count);
          if (status == ResourceLoader.ThreadLoadStatus.Loaded) break;
          if (status != ResourceLoader.ThreadLoadStatus.InProgress)
            throw new InvalidOperationException($"{path}: {status}");
          await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }
        var resource = ResourceLoader.LoadThreadedGet(path);
        _resources.Add(resource); // Retain cached resources through scene initialization.
        if (path == gameType.GameScenePath) target = resource as PackedScene;
        SetProgress(90.0 * (i + 1) / paths.Count);
        await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
      }

      if (target == null) throw new InvalidOperationException("Game scene did not load.");
      SetProgress(95);
      await DrawFrame();
      beforeSwitch?.Invoke();
      var sceneError = tree.ChangeSceneToPacked(target);
      if (sceneError != Error.Ok) throw new InvalidOperationException($"Scene change: {sceneError}");
      await ToSignal(tree, SceneTree.SignalName.SceneChanged);
      await DrawFrame();
      var camera = tree.CurrentScene.GetViewport().GetCamera3D();
      if (camera != null) {
        var previews = new List<PackedScene>();
        foreach (var faction in FactionCatalog.FactionDefs) {
          previews.Add(GD.Load<PackedScene>(faction.HeroScenePath));
          previews.Add(GD.Load<PackedScene>(faction.MinionScenePath));
        }
        previews.Add(GD.Load<PackedScene>(Scenes.Turret));
        previews.Add(GD.Load<PackedScene>(Scenes.Crystal));
        previews.Add(GD.Load<PackedScene>(Scenes.Container));
        await HoverOutlineRenderer.ForCamera(camera).PrewarmAsync(previews, fraction => SetProgress(95 + fraction * 4));
      }
      SetProgress(100);
      await DrawFrame();
      QueueFree();
    }
    catch (Exception error) {
      GD.PushError($"[LoadingScreen] {error}");
      _percentage.Text = "Loading failed";
      _dismiss.Show();
      _dismiss.Pressed += () => {
        _active = null;
        if (IsInstanceValid(source)) source.ProcessMode = sourceProcessMode;
        onFailure?.Invoke();
        QueueFree();
      };
    }
  }

  private void SetProgress(double value) {
    _bar.Value = Math.Max(_bar.Value, value);
    _percentage.Text = $"{(int)_bar.Value}%";
  }

  private async Task DrawFrame() {
    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    if (DisplayServer.GetName() != "headless")
      await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
  }

  private static List<string> ResourcePaths(GameTypeCatalog.GameTypeDef gameType) {
    var paths = new List<string>();
    var seen = new HashSet<string>();
    void Add(string path) {
      if (!string.IsNullOrEmpty(path) && seen.Add(path)) paths.Add(path);
    }

    foreach (var skill in SkillCatalog.SkillDefs) Add(skill.IconTexturePath);
    foreach (var item in ShopItemCatalog.ItemDefs) Add(item.IconTexturePath);
    foreach (var faction in FactionCatalog.FactionDefs) {
      Add(faction.HeroScenePath);
      Add(faction.MinionScenePath);
      Add(faction.HeroPortraitTexturePath);
      Add(faction.MinionPortraitTexturePath);
    }
    foreach (var path in SkillEffectCatalog.ScenePaths) Add(path);
    foreach (var path in HoverOutlineRenderer.PreloadPaths) Add(path);
    Add(Scenes.Container);
    Add(Scenes.Turret);
    Add(Scenes.Crystal);
    Add(Scenes.WaterBottle);
    Add(Scenes.Oasis);
    Add(gameType.WorldScenePath);
    Add(gameType.GameScenePath);
    return paths;
  }

  public override void _ExitTree() {
    if (_active == this) _active = null;
    _resources.Clear();
  }
}
