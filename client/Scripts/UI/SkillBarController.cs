using System;
using Godot;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using Meesles.Avalon.Sim.Heroes;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon;

// Owns the first four cells of GameUI's ActionGrid, one per SkillSlot in slot order. ActionBarController
// owns everything after them (see its reservedLeadingCells) - these two share one GridContainer, split by
// index, so this controller builds its cells once and never adds or removes a child afterwards.
//
// Each cell is a coloured rect with the skill's current rank centred on it, and a flat transparent Button
// on top that spends a point. Colour carries the only state the player needs at a glance: coloured means
// the slot can be ranked up right now, grey means it cannot - either the hero has no points banked or the
// slot is already at MaxRank. The sim re-checks both when the UpgradeSkillCommand lands, so a cell that is
// briefly optimistic is harmless.
public class SkillBarController {
  private const float CellSize = 58f;
  public const int SlotCount = SkillsComponent.MaxSlots;

  // Indexed by SkillSlot. Distinct hues so the four slots stay tellable apart before they have icons.
  private static readonly Color[] SlotColors = [
    new(0.82f, 0.36f, 0.30f, 0.88f),
    new(0.38f, 0.72f, 0.44f, 0.88f),
    new(0.36f, 0.55f, 0.86f, 0.88f),
    new(0.78f, 0.62f, 0.26f, 0.88f)
  ];

  private static readonly Color InactiveColor = new(0.28f, 0.29f, 0.32f, 0.85f);

  // Border shown on every cell while the hero has an unspent skill point, so the "go spend it" cue is
  // visible without hunting for which slot lit up. Slot colour still says which ones can take it.
  private const int PointHintBorderWidth = 3;
  private static readonly Color PointHintBorderColor = new(0.98f, 0.82f, 0.22f, 1f);

  private static readonly string[] HotkeyLabels = ["Q", "W", "E", "R"];

  private readonly SkillCatalog _catalog;
  private readonly Cell[] _cells = new Cell[SlotCount];
  private readonly GridContainer _grid;
  private readonly Action<int> _onUpgrade;
  private readonly PredictedSkillState _predicted;

  public SkillBarController(GridContainer grid, SkillCatalog catalog, Action<int> onUpgrade,
    PredictedSkillState predicted) {
    _grid = grid;
    _catalog = catalog;
    _onUpgrade = onUpgrade;
    _predicted = predicted;
    Build();
  }

  // Called every HUD sync (GameUI.SyncFromFrame). Repaints from SkillsComponent, which is the sim's own
  // record of ranks and banked points, so a rollback-corrected rank simply repaints on the next sync.
  public void Update(Frame frame, int? localPlayerId) {
    if (_grid == null) return;
    if (frame != null && localPlayerId is int playerId && TryPaintLocalHero(frame, playerId))
      return;

    // No local hero yet (pre-spawn, or spectating): show the slots inert rather than stale.
    for (var slot = 0; slot < SlotCount; slot++)
      Paint(slot, 0, 0, false, 0, false);
  }

  private bool TryPaintLocalHero(Frame frame, int playerId) {
    var filter = frame.Filter<Hero, SkillsComponent>();
    while (filter.Next(out var entity)) {
      ref readonly var hero = ref frame.GetReadOnly<Hero>(entity);
      if (hero.PlayerId != playerId)
        continue;

      ref readonly var skills = ref frame.GetReadOnly<SkillsComponent>(entity);

      // Retire or age every slot's optimistic entry before anything reads it, so PendingPoints below
      // is the count still genuinely in flight.
      for (var slot = 0; slot < SlotCount; slot++)
        _predicted?.Observe(slot, skills.GetRank(slot));

      var pendingPoints = _predicted?.PendingPoints ?? 0;
      var hasPoints = skills.SkillPoints - pendingPoints > 0;

      for (var slot = 0; slot < SlotCount; slot++) {
        var skillAssetId = skills.GetSkillAssetId(slot);
        var pendingRanks = _predicted?.OutstandingFor(slot) ?? 0;
        var rank = skills.GetRank(slot) + pendingRanks;
        var maxRank = GetMaxRank(frame, skillAssetId);
        var active = SkillActions.CanUpgrade(ref frame, playerId, slot, pendingPoints, pendingRanks);
        Paint(slot, rank, maxRank, active, skillAssetId, hasPoints);
      }

      return true;
    }

    return false;
  }

  private static int GetMaxRank(Frame frame, int skillAssetId) {
    return frame.AssetRegistry.TryGet<SkillAsset>(skillAssetId, out var asset) ? asset.MaxRank : 0;
  }

  // Cheap early-out on the values that actually drive the cell, so a steady-state sync does no string
  // formatting and no Godot property writes.
  private void Paint(int slot, int rank, int maxRank, bool active, int skillAssetId, bool hasPoints) {
    var cell = _cells[slot];
    if (cell == null) return;
    if (cell.Rank == rank && cell.MaxRank == maxRank && cell.Active == active
        && cell.SkillAssetId == skillAssetId && cell.HasPoints == hasPoints)
      return;

    cell.Rank = rank;
    cell.MaxRank = maxRank;
    cell.Active = active;
    cell.SkillAssetId = skillAssetId;
    cell.HasPoints = hasPoints;

    cell.Rect.Color = active ? SlotColors[slot] : InactiveColor;
    cell.PointHint.Visible = hasPoints;
    cell.Label.Text = rank.ToString();
    cell.Button.Disabled = !active;
    cell.Button.TooltipText = BuildTooltip(slot, rank, maxRank, skillAssetId);
  }

  private string BuildTooltip(int slot, int rank, int maxRank, int skillAssetId) {
    var name = _catalog != null && _catalog.TryResolve(skillAssetId, out var def)
      ? def.Name
      : ((SkillSlot)slot).ToString();
    return maxRank > 0 ? $"{name}\nRank {rank} / {maxRank}" : name;
  }

  // The .tscn authors placeholder cells into ActionGrid; drop them all and lay down the four skill cells
  // first, so ActionBarController's reserved-cell count lines up with our indices.
  private void Build() {
    if (_grid == null) return;

    foreach (var child in _grid.GetChildren()) {
      _grid.RemoveChild(child);
      child.QueueFree();
    }

    for (var slot = 0; slot < SlotCount; slot++)
      _cells[slot] = CreateCell(slot);
  }

  private Cell CreateCell(int slot) {
    var rect = new ColorRect {
      Name = $"Skill_{(SkillSlot)slot}",
      CustomMinimumSize = new Vector2(CellSize, CellSize),
      Color = InactiveColor
    };

    // Flat and textless, so it contributes only input and the hover/pressed highlight; the rect behind it
    // and the label above it do the drawing.
    var button = new Button { Name = "Upgrade", Flat = true, Disabled = true };
    button.SetAnchorsPreset(Control.LayoutPreset.FullRect);
    var captured = slot;
    button.Pressed += () => _onUpgrade?.Invoke(captured);
    rect.AddChild(button);

    var pointHint = CreatePointHint();
    rect.AddChild(pointHint);

    var label = new Label {
      Name = "Rank",
      Text = "0",
      MouseFilter = Control.MouseFilterEnum.Ignore,
      HorizontalAlignment = HorizontalAlignment.Center,
      VerticalAlignment = VerticalAlignment.Center
    };
    label.SetAnchorsPreset(Control.LayoutPreset.FullRect);
    label.AddThemeFontSizeOverride("font_size", 20);
    label.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f));
    label.AddThemeConstantOverride("outline_size", 3);
    rect.AddChild(label);

    AddHotkeyBadge(rect, slot);
    _grid.AddChild(rect);
    return new Cell(rect, label, button, pointHint);
  }

  // Border-only stylebox over the cell's fill: transparent background, so the slot colour underneath
  // still reads through.
  private static Panel CreatePointHint() {
    var style = new StyleBoxFlat { BgColor = new Color(0f, 0f, 0f, 0f), BorderColor = PointHintBorderColor };
    style.SetBorderWidthAll(PointHintBorderWidth);

    var panel = new Panel {
      Name = "PointHint",
      MouseFilter = Control.MouseFilterEnum.Ignore,
      Visible = false
    };
    panel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
    panel.AddThemeStyleboxOverride("panel", style);
    return panel;
  }

  // The cast hotkey pinned to the cell's top-left, so the Q/W/E/R binding is visible without a tooltip.
  // Mirrors the shop grid's cost badge. Kept in slot order alongside InputCapture.TryGetSkillHotkeySlot.
  private static void AddHotkeyBadge(ColorRect rect, int slot) {
    var badge = new Label {
      Name = "Hotkey",
      Text = HotkeyLabels[slot],
      MouseFilter = Control.MouseFilterEnum.Ignore,
      HorizontalAlignment = HorizontalAlignment.Left,
      VerticalAlignment = VerticalAlignment.Top
    };
    badge.SetAnchorsPreset(Control.LayoutPreset.FullRect);
    badge.AddThemeFontSizeOverride("font_size", 12);
    badge.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f));
    badge.AddThemeConstantOverride("outline_size", 3);
    rect.AddChild(badge);
  }

  private sealed class Cell(ColorRect rect, Label label, Button button, Panel pointHint) {
    public readonly Button Button = button;
    public readonly Label Label = label;
    public readonly Panel PointHint = pointHint;
    public readonly ColorRect Rect = rect;

    // Last painted state. -1 so the first Paint always writes through.
    public bool Active;
    public bool HasPoints;
    public int MaxRank = -1;
    public int Rank = -1;
    public int SkillAssetId = -1;
  }
}
