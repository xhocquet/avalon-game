using System.Collections.Generic;
using Godot;
using Meesles.Avalon.Sim;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon;

public sealed class BuffBarController {
  private static readonly PackedScene IconScene = GD.Load<PackedScene>("res://Scenes/UI/Icons/BuffIcon.tscn");
  private readonly HBoxContainer _container;
  private readonly SkillCatalog _catalog;
  private readonly List<StatusEffect> _effects = new(32);
  private readonly List<Cell> _cells = new();
  private readonly Vector2 _cellSize = new(34, 34);
  private readonly Texture2D _fallbackTexture;

  public BuffBarController(HBoxContainer container, SkillCatalog catalog) {
    _container = container;
    _catalog = catalog;
    if (_container == null)
      return;

    var template = _container.GetChildCount() > 0 ? _container.GetChild<Control>(0) : null;
    if (template != null && template.CustomMinimumSize.X > 0 && template.CustomMinimumSize.Y > 0)
      _cellSize = template.CustomMinimumSize;
    _fallbackTexture = (template as TextureRect)?.Texture
      ?? GD.Load<Texture2D>("res://Assets/Placeholders/red-swirl.webp");

    foreach (var child in _container.GetChildren()) {
      _container.RemoveChild(child);
      child.QueueFree();
    }
  }

  public void Update(Frame frame, int? localPlayerId) {
    if (_container == null)
      return;
    if (frame == null || localPlayerId is not int playerId ||
        !UnitLookup.TryGetPlayerHero(ref frame, playerId, out var hero)) {
      Clear();
      return;
    }

    StatusEffects.Collect(ref frame, hero, _effects);
    for (var i = 0; i < _effects.Count; i++) {
      var cell = i < _cells.Count ? _cells[i] : CreateCell();
      var skillId = _effects[i].SourceSkillAssetId;
      if (cell.SkillAssetId != skillId) {
        cell.SkillAssetId = skillId;
        cell.Icon.Texture = _catalog?.ResolveIcon(skillId) ?? _fallbackTexture;
      }
      cell.SetElapsedFill(ElapsedFraction(_effects[i].Timing, frame.Tick));
      if (!cell.Icon.Visible)
        cell.Icon.Show();
    }
    HideUnused(_effects.Count);
  }

  public void Clear() {
    _effects.Clear();
    HideUnused(0);
  }

  private static float ElapsedFraction(StatusEffectTiming timing, int tick) {
    var remaining = timing.GetRemainingFraction(tick);
    return remaining.HasValue ? 1f - remaining.Value.ToFloat() : 0f;
  }

  private Cell CreateCell() {
    var icon = IconScene.Instantiate<TextureRect>();
    icon.Name = $"BuffIcon_{_cells.Count}";
    icon.CustomMinimumSize = _cellSize;
    var shroud = icon.GetNode<ColorRect>("LifetimeShroud");
    _container.AddChild(icon);
    var cell = new Cell(icon, shroud);
    _cells.Add(cell);
    return cell;
  }

  private void HideUnused(int activeCount) {
    for (var i = activeCount; i < _cells.Count; i++)
      if (_cells[i].Icon.Visible)
        _cells[i].Icon.Hide();
  }

  private sealed class Cell(TextureRect icon, ColorRect shroud) {
    public readonly TextureRect Icon = icon;
    public int SkillAssetId = -1;
    private float _elapsedFill = -1f;

    public void SetElapsedFill(float fill) {
      fill = Mathf.Clamp(fill, 0f, 1f);
      if (_elapsedFill == fill)
        return;
      _elapsedFill = fill;
      shroud.AnchorTop = 1f - fill;
      shroud.Visible = fill > 0f;
    }
  }
}
