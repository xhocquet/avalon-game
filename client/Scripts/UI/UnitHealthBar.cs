using Godot;

namespace Meesles.Avalon;

public partial class UnitHealthBar : Control {
  public enum BarTier { Minion, Turret, Hero }

  [Export] public BarTier Tier { get; set; }
  [Export] public float BarWidth { get; set; } = 54f;
  [Export] public float HealthHeight { get; set; } = 6f;
  [Export] public float ManaHeight { get; set; } = 3f;
  [Export] public float HealthPerChunk { get; set; } = 100f;
  [Export] public float LargeChunkThreshold { get; set; } = 1500f;
  [Export] public float LargeHealthPerChunk { get; set; } = 500f;
  [Export] public Color BackgroundColor { get; set; } = new("191c20");
  [Export] public Color WoodColor { get; set; } = new("62442c");
  [Export] public Color GoldColor { get; set; } = new("b99553");
  [Export] public Color ManaColor { get; set; } = new("479fea");

  private float _health;
  private float _maxHealth = 100f;
  private float _mana;
  private float _maxMana;
  private int _level = 1;
  private string _playerName = "";
  private Color _fillColor = new("45bb68");

  public override void _Ready() {
    MouseFilter = MouseFilterEnum.Ignore;
  }

  public void SetState(float health, float maxHealth, float mana, float maxMana, int level, string playerName,
    Color fillColor) {
    if (_health == health && _maxHealth == maxHealth && _mana == mana && _maxMana == maxMana
        && _level == level && _playerName == playerName && _fillColor == fillColor) return;
    _health = health;
    _maxHealth = maxHealth;
    _mana = mana;
    _maxMana = maxMana;
    _level = level;
    _playerName = playerName;
    _fillColor = fillColor;
    QueueRedraw();
  }

  public override void _Draw() {
    var hero = Tier == BarTier.Hero;
    var ornate = Tier != BarTier.Minion;
    var rim = ornate ? 3f : 1f;
    var totalHeight = HealthHeight + (hero ? ManaHeight + 1f : 0f);
    var bounds = new Rect2(0, 0, BarWidth, totalHeight + rim * 2);
    DrawRect(bounds, Colors.Black);
    if (ornate) {
      DrawRect(bounds.Grow(-1f), WoodColor);
      DrawRect(new Rect2(2, 1, BarWidth - 4, 1), GoldColor);
      DrawRect(new Rect2(2, bounds.Size.Y - 2, BarWidth - 4, 1), GoldColor.Darkened(0.35f));
    }

    var hp = new Rect2(rim, rim, BarWidth - rim * 2, HealthHeight);
    DrawRect(hp, BackgroundColor);
    var ratio = _maxHealth > 0 ? Mathf.Clamp(_health / _maxHealth, 0f, 1f) : 0f;
    var fillWidth = hp.Size.X * ratio;
    DrawRect(new Rect2(hp.Position, new Vector2(fillWidth, hp.Size.Y)), _fillColor);
    var heavyChunks = _maxHealth > LargeChunkThreshold;
    if (heavyChunks) {
      for (var y = 0; y < hp.Size.Y; y++) {
        for (var stripe = -hp.Size.Y; stripe < fillWidth; stripe += 8f) {
          var left = Mathf.Max(0f, stripe + y);
          var right = Mathf.Min(fillWidth, stripe + y + 3f);
          if (right > left)
            DrawRect(new Rect2(hp.Position + new Vector2(left, y), new Vector2(right - left, 1)), new Color(1, 1, 1, 0.7f));
        }
      }
    }
    if (ornate && ratio > 0f)
      DrawRect(new Rect2(hp.Position, new Vector2(hp.Size.X * ratio, 1)), _fillColor.Lightened(0.3f));

    var healthPerChunk = heavyChunks ? LargeHealthPerChunk : HealthPerChunk;
    if (healthPerChunk > 0f && _maxHealth > 0f) {
      var previousPixel = -1;
      for (var chunk = 1; chunk < Mathf.CeilToInt(_maxHealth / healthPerChunk); chunk++) {
        var pixel = Mathf.RoundToInt(hp.Size.X * (chunk * healthPerChunk / _maxHealth));
        if (pixel <= 0 || pixel >= hp.Size.X || pixel == previousPixel) continue;
        DrawRect(new Rect2(hp.Position + new Vector2(pixel, 0), new Vector2(1, hp.Size.Y)), new Color(0, 0, 0, 0.8f));
        previousPixel = pixel;
      }
    }

    if (!hero) return;
    var mana = new Rect2(rim, rim + HealthHeight + 1, hp.Size.X, ManaHeight);
    DrawRect(mana, BackgroundColor);
    var manaRatio = _maxMana > 0 ? Mathf.Clamp(_mana / _maxMana, 0f, 1f) : 0f;
    DrawRect(new Rect2(mana.Position, new Vector2(mana.Size.X * manaRatio, mana.Size.Y)), ManaColor);

    var badgeSize = bounds.Size.Y;
    var badge = new Rect2(-badgeSize + 1, 0, badgeSize, badgeSize);
    DrawRect(badge, Colors.Black);
    DrawRect(badge.Grow(-1), GoldColor);
    DrawRect(badge.Grow(-2), WoodColor.Darkened(0.55f));
    var font = ThemeDB.FallbackFont;
    var text = _level.ToString();
    var textSize = font.GetStringSize(text, fontSize: 12);
    var baseline = badge.Position + new Vector2((badgeSize - textSize.X) * 0.5f,
      (badgeSize - font.GetHeight(12)) * 0.5f + font.GetAscent(12));
    DrawString(font, baseline.Round(), text, fontSize: 12, modulate: new Color("f5e5bf"));

    var nameWidth = BarWidth + badgeSize - 1;
    var nameSize = font.GetStringSize(_playerName, fontSize: 13);
    var namePosition = new Vector2(-badgeSize + 1 + Mathf.Max(0, (nameWidth - nameSize.X) * 0.5f), -5);
    DrawStringOutline(font, namePosition.Round(), _playerName, width: nameWidth, fontSize: 13, size: 3,
      modulate: Colors.Black);
    DrawString(font, namePosition.Round(), _playerName, width: nameWidth, fontSize: 13, modulate: Colors.White);
  }
}
