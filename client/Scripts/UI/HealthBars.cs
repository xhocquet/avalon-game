using System;
using System.Collections.Generic;
using Godot;
using Meesles.Avalon.Client;
using Meesles.Avalon.Client.Scripts.Entities;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Godot;

namespace Meesles.Avalon;

public partial class HealthBars : Control {
  [Export] public PackedScene MinionBarScene { get; set; } = GD.Load<PackedScene>("res://Scenes/UI/HealthBars/MinionHealthBar.tscn");
  [Export] public PackedScene TurretBarScene { get; set; } = GD.Load<PackedScene>("res://Scenes/UI/HealthBars/TurretHealthBar.tscn");
  [Export] public PackedScene HeroBarScene { get; set; } = GD.Load<PackedScene>("res://Scenes/UI/HealthBars/HeroHealthBar.tscn");
  [Export] public float BarWorldYOffset { get; set; } = 1.5f;
  [Export] public float BarScreenOffsetY { get; set; } = -10f;
  [Export] public Color TeamOneFillColor { get; set; } = TeamPalette.Default.TeamOne;
  [Export] public Color TeamTwoFillColor { get; set; } = TeamPalette.Default.TeamTwo;
  [Export] public Color NeutralFillColor { get; set; } = TeamPalette.Default.Neutral;

  public Func<int, string> ResolvePlayerName { get; set; }
  public Func<int, string> ResolvePlayerNotice { get; set; }

  private readonly Dictionary<ulong, UnitHealthBar> _bars = new();
  private readonly Dictionary<UnitHealthBar.BarTier, Stack<UnitHealthBar>> _idle = new() {
    [UnitHealthBar.BarTier.Minion] = new(),
    [UnitHealthBar.BarTier.Turret] = new(),
    [UnitHealthBar.BarTier.Hero] = new()
  };
  private readonly HashSet<ulong> _seen = new();
  private readonly List<ulong> _removed = new();

  public override void _Ready() {
    MouseFilter = MouseFilterEnum.Ignore;
  }

  public override void _Process(double delta) {
    _seen.Clear();
    var cam = GetViewport().GetCamera3D();
    var viewport = GetViewport().GetVisibleRect();
    foreach (var node in GetTree().GetNodesInGroup("units")) {
      if (cam == null || node is not EntityViewNode view || !view.IsVisibleInTree()) continue;
      var frame = view.Engine?.PredictedFrame.Frame;
      var entity = view.EntityRef;
      if (frame == null || !entity.IsValid || !frame.Has<Health>(entity) || !frame.Has<Stats>(entity)) continue;

      ref readonly var health = ref frame.GetReadOnly<Health>(entity);
      ref readonly var stats = ref frame.GetReadOnly<Stats>(entity);
      var maxHealth = stats.MaxHealth.ToFloat();
      if (!health.IsAlive || maxHealth <= 0f) continue;

      var isHero = frame.Has<Hero>(entity);
      var isTurret = frame.Has<Turret>(entity);
      var height = BarWorldYOffset;
      var level = 1;
      var playerName = "";
      if (isHero) {
        ref readonly var hero = ref frame.GetReadOnly<Hero>(entity);
        height = frame.AssetRegistry.Get<HeroStatsAsset>(hero.HeroStatsAssetId).HealthBarHeight.ToFloat();
        level = frame.Has<Experience>(entity) ? frame.GetReadOnly<Experience>(entity).Level : 1;
        playerName = ResolvePlayerNotice?.Invoke(hero.PlayerId)
          ?? ResolvePlayerName?.Invoke(hero.PlayerId) ?? $"Player {hero.PlayerId}";
      }
      else if (view is MinionEntity minion) {
        height = minion.HealthBarHeight >= 0f ? minion.HealthBarHeight
          : frame.AssetRegistry.Get<MinionStatsAsset>().HealthBarHeight.ToFloat();
      }
      else if (view is TurretEntity turret) {
        height = turret.HealthBarHeight;
      }

      var worldPoint = view.GlobalPosition + Vector3.Up * height;
      if (cam.IsPositionBehind(worldPoint)) continue;
      var point = cam.UnprojectPosition(worldPoint) - viewport.Position;
      if (!new Rect2(Vector2.Zero, viewport.Size).Grow(150f).HasPoint(point)) continue;

      var key = view.GetInstanceId();
      _seen.Add(key);
      var tier = isHero ? UnitHealthBar.BarTier.Hero
        : isTurret ? UnitHealthBar.BarTier.Turret : UnitHealthBar.BarTier.Minion;
      if (_bars.TryGetValue(key, out var bar) && bar.Tier != tier) {
        ReturnBar(bar);
        bar = null;
      }
      if (bar == null) {
        bar = RentBar(tier);
        _bars[key] = bar;
      }
      var teamId = frame.Has<Team>(entity) ? frame.GetReadOnly<Team>(entity).TeamId : 0;
      var color = teamId == 1 ? TeamOneFillColor : teamId == 2 ? TeamTwoFillColor : NeutralFillColor;
      bar.Position = (point + new Vector2(-bar.BarWidth * 0.5f, BarScreenOffsetY)).Round();
      bar.SetState(health.Current.ToFloat(), maxHealth, health.Mana.ToFloat(), stats.MaxMana.ToFloat(), level,
        playerName, color);
      bar.Show();
    }

    _removed.Clear();
    foreach (var (key, bar) in _bars) {
      if (_seen.Contains(key)) continue;
      ReturnBar(bar);
      _removed.Add(key);
    }
    foreach (var key in _removed) _bars.Remove(key);
  }

  private UnitHealthBar RentBar(UnitHealthBar.BarTier tier) {
    var pool = _idle[tier];
    if (pool.Count > 0) return pool.Pop();
    var scene = tier switch {
      UnitHealthBar.BarTier.Hero => HeroBarScene,
      UnitHealthBar.BarTier.Turret => TurretBarScene,
      _ => MinionBarScene
    };
    var bar = scene.Instantiate<UnitHealthBar>();
    bar.Hide();
    AddChild(bar);
    return bar;
  }

  private void ReturnBar(UnitHealthBar bar) {
    bar.Hide();
    _idle[bar.Tier].Push(bar);
  }
}
