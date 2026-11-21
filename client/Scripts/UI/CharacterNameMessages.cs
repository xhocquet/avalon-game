using System;
using System.Collections.Generic;
using Meesles.Avalon.Sim;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon;

public sealed class CharacterNameMessages {
  [Flags]
  private enum MessageKind {
    None = 0, Slowed = 1, Weakened = 2, Silenced = 4, Snared = 8,
    DefenseUp = 16, SpeedUp = 32, DamageUp = 64, FasterAttacks = 128
  }

  private static readonly (MessageKind Kind, string Text)[] Labels = [
    (MessageKind.Slowed, "Slowed!"), (MessageKind.Weakened, "Weakened!"),
    (MessageKind.Silenced, "Silenced!"), (MessageKind.Snared, "Snared!"),
    (MessageKind.DefenseUp, "Defense Up!"), (MessageKind.SpeedUp, "Speed Up!"),
    (MessageKind.DamageUp, "Damage Up!"), (MessageKind.FasterAttacks, "Faster Attacks!")
  ];
  private readonly List<StatusEffect> _effects = new();
  private readonly List<string> _messages = new();
  private HashSet<Application> _previous = new();
  private HashSet<Application> _active = new();
  private string _current;
  private double _nextChange;
  private EntityRef _character;

  private readonly record struct Application(StatusEffectKey Key, int SkillId, int CasterId, MessageKind Kind);

  public string Resolve(Frame frame, EntityRef character, bool hasSkillUpgrade, string playerName, double time,
    double cleansedUntil = 0) {
    if (_character != character) {
      Reset();
      _character = character;
    }
    StatusEffects.Collect(ref frame, character, _effects);
    _messages.Clear();
    _active.Clear();
    string newest = null;
    var newestTick = int.MinValue;
    foreach (var effect in _effects) {
      var kinds = GetMessageKinds(effect);
      foreach (var (kind, text) in Labels) {
        if ((kinds & kind) == 0) continue;
        if (!_messages.Contains(text)) _messages.Add(text);
        var application = new Application(effect.Key, effect.SourceSkillAssetId, effect.SourceUnitId, kind);
        _active.Add(application);
        var started = effect.Timing.StartedTick ?? int.MinValue;
        if (!_previous.Contains(application) && started >= newestTick) {
          newest = text;
          newestTick = started;
        }
      }
    }
    (_previous, _active) = (_active, _previous);

    if (_messages.Count == 0) {
      _current = null;
      if (time < cleansedUntil) return "Cleansed!";
      return hasSkillUpgrade ? "Upgrade skill!" : playerName;
    }
    if (newest != null || !_messages.Contains(_current)) {
      _current = newest ?? _messages[0];
      _nextChange = time + 1.0;
    }
    else if (time >= _nextChange) {
      var steps = Math.Floor(time - _nextChange) + 1;
      var index = (_messages.IndexOf(_current) + (int)(steps % _messages.Count)) % _messages.Count;
      _current = _messages[index];
      _nextChange += steps;
    }
    if (time < cleansedUntil) {
      _nextChange = cleansedUntil + 1.0;
      return "Cleansed!";
    }
    return _current;
  }

  private static MessageKind GetMessageKinds(StatusEffect effect) {
    if (effect.Kind == StatusEffectKind.Silence) return MessageKind.Silenced;
    if (effect.Kind == StatusEffectKind.Snare) return MessageKind.Snared;
    if (effect.Kind == StatusEffectKind.AttackProc) return MessageKind.DamageUp;
    if (effect.Kind != StatusEffectKind.Buff) return MessageKind.None;
    var kinds = MessageKind.None;
    foreach (var change in effect.Changes) {
      if (!change.Change.IsStat) continue;
      kinds |= (change.Polarity, (StatType)change.Change.Parameter) switch {
        (EffectPolarity.Debuff, StatType.MoveSpeed) => MessageKind.Slowed,
        (EffectPolarity.Debuff, StatType.Armor or StatType.MagicResist) => MessageKind.Weakened,
        (EffectPolarity.Buff, StatType.Armor or StatType.MagicResist) => MessageKind.DefenseUp,
        (EffectPolarity.Buff, StatType.MoveSpeed) => MessageKind.SpeedUp,
        (EffectPolarity.Buff, StatType.AttackDamage) => MessageKind.DamageUp,
        (EffectPolarity.Buff, StatType.BaseAttackSpeed or StatType.BonusAttackSpeed) => MessageKind.FasterAttacks,
        _ => MessageKind.None
      };
    }
    return kinds;
  }

  public void Reset() {
    _previous.Clear();
    _active.Clear();
    _effects.Clear();
    _messages.Clear();
    _current = null;
    _nextChange = 0;
    _character = default;
  }
}
