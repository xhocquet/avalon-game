using System;
using System.Collections.Generic;
using Godot;

namespace Meesles.Avalon;

public partial class VaultPage : Control {
  private readonly List<Button> _heroButtons = [];
  private ChampionPreview _preview;
  private Label _heroName;
  private Label _animationName;
  private Button _previousAnimation;
  private Button _nextAnimation;
  private string[] _animations = [];
  private int _animationIndex;

  public override void _Ready() {
    _preview = GetNode<ChampionPreview>("%Preview");
    _heroName = GetNode<Label>("%HeroName");
    _animationName = GetNode<Label>("%AnimationName");
    _previousAnimation = GetNode<Button>("%PreviousAnimation");
    _nextAnimation = GetNode<Button>("%NextAnimation");
    _previousAnimation.Pressed += () => CycleAnimation(-1);
    _nextAnimation.Pressed += () => CycleAnimation(1);

    var column = GetNode<VBoxContainer>("%HeroButtons");
    var group = new ButtonGroup();
    foreach (var def in FactionCatalog.FactionDefs) {
      var button = new Button {
        Text = def.Name,
        CustomMinimumSize = new Vector2(0, 56),
        ThemeTypeVariation = "LobbyButton",
        ToggleMode = true,
        ButtonGroup = group
      };
      button.AddThemeFontSizeOverride("font_size", 24);
      var border = new NinePatchRect {
        Texture = GD.Load<Texture2D>("res://Assets/UI/PanelBorderBrown.png"),
        ShowBehindParent = true,
        MouseFilter = MouseFilterEnum.Ignore,
        DrawCenter = false,
        PatchMarginLeft = 32,
        PatchMarginTop = 32,
        PatchMarginRight = 32,
        PatchMarginBottom = 32,
        Visible = false
      };
      button.AddChild(border);
      border.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
      border.OffsetLeft = border.OffsetTop = -8;
      border.OffsetRight = border.OffsetBottom = 8;
      button.Toggled += selected => border.Visible = selected;
      var index = _heroButtons.Count;
      button.Pressed += () => SelectHero(index);
      column.AddChild(button);
      _heroButtons.Add(button);
    }

    if (_heroButtons.Count > 0) SelectHero(0);
  }

  private void SelectHero(int index) {
    var def = FactionCatalog.FactionDefs[index];
    _heroButtons[index].ButtonPressed = true;
    _heroName.Text = def.Name;
    _preview.ShowFaction(def.Id);
    _animations = Array.FindAll(_preview.GetAnimationNames(), name =>
      !name.Equals("RESET", StringComparison.OrdinalIgnoreCase));
    _animationIndex = Array.FindIndex(_animations, name => name.Contains("idle", StringComparison.OrdinalIgnoreCase));
    if (_animationIndex < 0) _animationIndex = 0;
    _previousAnimation.Disabled = _nextAnimation.Disabled = _animations.Length < 2;
    PlayAnimation();
  }

  private void CycleAnimation(int direction) {
    if (_animations.Length == 0) return;
    _animationIndex = (_animationIndex + direction + _animations.Length) % _animations.Length;
    PlayAnimation();
  }

  private void PlayAnimation() {
    _animationName.Text = _animations.Length == 0 ? "No animations" : _animations[_animationIndex];
    if (_animations.Length > 0) _preview.PlayAnimation(_animations[_animationIndex]);
  }
}
