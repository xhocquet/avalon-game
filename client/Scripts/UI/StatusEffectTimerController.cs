using System.Collections.Generic;
using Meesles.Avalon.Sim;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon;

public readonly record struct StatusEffectTimer(string Caption, double RemainingFraction, double RemainingSeconds);

public sealed class StatusEffectTimerController {
  private readonly record struct Rule(StatusEffectKind Kind, string Caption, StatType? DebuffedStat = null);

  private readonly List<Rule> _enabled = new();
  private readonly List<StatusEffect> _effects = new();

  public void Enable(StatusEffectKind kind, string caption) => _enabled.Add(new Rule(kind, caption));

  public void EnableDebuff(StatType stat, string caption) =>
    _enabled.Add(new Rule(StatusEffectKind.Buff, caption, stat));

  public bool TryResolve(Frame frame, EntityRef character, out StatusEffectTimer timer) {
    timer = default;
    if (_enabled.Count == 0) return false;
    StatusEffects.Collect(ref frame, character, _effects);
    foreach (var rule in _enabled) {
      StatusEffect? selected = null;
      foreach (var effect in _effects) {
        if (!Matches(effect, rule) || !effect.Timing.GetRemainingFraction(frame.Tick).HasValue) continue;
        if (!selected.HasValue || effect.Timing.ExpiryTick > selected.Value.Timing.ExpiryTick)
          selected = effect;
      }
      if (!selected.HasValue) continue;
      var timing = selected.Value.Timing;
      timer = new StatusEffectTimer(rule.Caption, timing.GetRemainingFraction(frame.Tick).Value.ToFloat(),
        timing.GetRemainingTicks(frame.Tick) * (double)TickMath.DeltaTimeMs(ref frame) / 1000.0);
      return true;
    }
    return false;
  }

  private static bool Matches(StatusEffect effect, Rule rule) {
    if (effect.Kind != rule.Kind) return false;
    if (!rule.DebuffedStat.HasValue) return true;
    foreach (var change in effect.Changes) {
      if (change.Polarity == EffectPolarity.Debuff && change.Change.IsStat &&
          change.Change.Parameter == (int)rule.DebuffedStat.Value)
        return true;
    }
    return false;
  }
}
