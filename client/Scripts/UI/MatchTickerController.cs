using System;
using System.Collections.Generic;
using Godot;

namespace Meesles.Avalon.Client;

public sealed class MatchTickerController : IDisposable {
  private readonly VBoxContainer _rows;
  private readonly PackedScene _rowScene = GD.Load<PackedScene>("res://Scenes/UI/MatchTickerRow.tscn");
  private readonly Dictionary<int, Texture2D> _portraits = new();
  private readonly List<(Control Row, Tween Tween)> _active = new();
  private MatchTimeline _timeline;
  private int _lastSequence;

  public int MaxRows { get; set; } = 5;
  public double HoldSeconds { get; set; } = 6;

  public MatchTickerController(VBoxContainer rows) {
    _rows = rows;
    foreach (var sample in rows.GetChildren()) {
      rows.RemoveChild(sample);
      sample.QueueFree();
    }
    rows.Show();
    foreach (var faction in FactionCatalog.FactionDefs)
      _portraits[faction.Id] = GD.Load<Texture2D>(faction.MinimapPortraitTexturePath);
  }

  public void Bind(MatchTimeline timeline) {
    if (_timeline != null) _timeline.Changed -= Refresh;
    Clear();
    _timeline = timeline;
    if (_timeline == null) return;
    _timeline.Changed += Refresh;
    Refresh();
  }

  private void Refresh() {
    var entries = _timeline.Entries;
    if (entries.Count == 0) { Clear(); return; }
    for (var i = Math.Max(0, entries.Count - MaxRows); i < entries.Count; i++) {
      var entry = entries[i];
      if (entry.Sequence <= _lastSequence) continue;
      Add(MatchTickerTemplates.Resolve(entry));
      _lastSequence = entry.Sequence;
    }
  }

  private void Add(MatchTickerMessage message) {
    while (_active.Count >= MaxRows) Remove(_active[0].Row);
    var row = _rowScene.Instantiate<Control>();
    _rows.AddChild(row);
    var participants = row.GetNode<HBoxContainer>("Content/Participants");
    SetParticipant(participants, "Actor", message.Actor);
    SetParticipant(participants, "Target", message.Target);
    participants.GetNode<Label>("EventText").Text = message.Text;

    var content = row.GetNode<Control>("Content");
    content.Position = new Vector2(-Mathf.Max(row.CustomMinimumSize.X, content.GetCombinedMinimumSize().X) - _rows.GlobalPosition.X, 0);
    var tween = row.CreateTween();
    _active.Add((row, tween));
    tween.TweenProperty(content, "position:x", 0f, 0.22).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
    tween.TweenInterval(HoldSeconds);
    tween.TweenProperty(row, "modulate:a", 0f, 0.6);
    tween.TweenCallback(Callable.From(() => Remove(row)));
  }

  private void SetParticipant(HBoxContainer participants, string prefix, MatchTimelineParticipant person) {
    var label = participants.GetNode<Label>($"{prefix}Name");
    var portrait = participants.GetNode<TextureRect>($"{prefix}Portrait");
    label.Visible = person.IsHero;
    label.Text = person.Name ?? "";
    var color = TeamPalette.Default.ForTeam(person.TeamId);
    label.AddThemeColorOverride("font_color", color);
    portrait.Visible = person.IsHero && _portraits.TryGetValue(person.FactionId, out _);
    portrait.Texture = _portraits.GetValueOrDefault(person.FactionId);
    var material = (ShaderMaterial)portrait.Material.Duplicate();
    material.SetShaderParameter("team_color", color);
    portrait.Material = material;
  }

  private void Remove(Control row) {
    var index = _active.FindIndex(item => item.Row == row);
    if (index < 0) return;
    _active[index].Tween.Kill();
    _active.RemoveAt(index);
    _rows.RemoveChild(row);
    row.QueueFree();
  }

  private void Clear() {
    while (_active.Count > 0) Remove(_active[0].Row);
    _lastSequence = 0;
  }

  public void Dispose() => Bind(null);
}
