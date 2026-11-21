using GdUnit4;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using static GdUnit4.Assertions;

namespace Meesles.Avalon.Client.Tests;

[TestSuite]
public class StatusEffectTimerTests {
  [TestCase("Armor pct -0.2", StatType.Armor, "Weakened")]
  [TestCase("MagicResist pct -0.2", StatType.MagicResist, "Weakened")]
  [TestCase("MoveSpeed pct -0.3", StatType.MoveSpeed, "Slowed")]
  public void StatDebuffs_CountDownWithoutEnablingPositiveBuffs(string specs, StatType stat, string caption) {
    var (frame, hero) = CreateCharacter();
    var timers = new StatusEffectTimerController();
    timers.EnableDebuff(stat, caption);
    BuffsController.Apply(ref frame, hero, 500,
      [new BuffSpec(stat, BuffMode.Flat, FP64.One, FP64.Zero)], 1, 80);
    AssertThat(timers.TryResolve(frame, hero, out _)).IsFalse();
    BuffsController.ClearAll(ref frame, hero);
    var skill = new SkillAsset(515) { BuffDurationMs = 4000, BuffStats = specs };
    AssertThat(BuffsController.ApplySkill(ref frame, hero, skill, 1)).IsTrue();
    AssertThat(timers.TryResolve(frame, hero, out var full)).IsTrue();
    AssertThat(full.Caption).IsEqual(caption);
    AssertThat(full.RemainingFraction).IsEqual(1.0);
    frame.Tick = 40;
    AssertThat(timers.TryResolve(frame, hero, out var half)).IsTrue();
    AssertThat(half.RemainingFraction).IsEqual(0.5);
    AssertThat(half.RemainingSeconds).IsEqual(2.0);
    frame.Tick = 80;
    AssertThat(timers.TryResolve(frame, hero, out _)).IsFalse();
  }

  [TestCase(StatusEffectKind.Snare, "Snared")]
  [TestCase(StatusEffectKind.DamageOverTime, "Damage over time")]
  public void SnareAndDot_CountDownFromTheirStoredLifetime(StatusEffectKind kind, string caption) {
    var (frame, hero) = CreateCharacter();
    var timers = new StatusEffectTimerController();
    timers.Enable(kind, caption);
    frame.Tick = 20;
    if (kind == StatusEffectKind.Snare)
      SnareController.Apply(ref frame, hero, 511, 80);
    else
      AssertThat(DamageOverTimeController.Apply(ref frame, hero, hero, 501, FP64.One, 80)).IsTrue();
    AssertThat(timers.TryResolve(frame, hero, out var full)).IsTrue();
    AssertThat(full.Caption).IsEqual(caption);
    AssertThat(full.RemainingFraction).IsEqual(1.0);
    AssertThat(full.RemainingSeconds).IsEqual(4.0);
    frame.Tick = 60;
    AssertThat(timers.TryResolve(frame, hero, out var half)).IsTrue();
    AssertThat(half.RemainingFraction).IsEqual(0.5);
    AssertThat(half.RemainingSeconds).IsEqual(2.0);
    frame.Tick = 100;
    AssertThat(timers.TryResolve(frame, hero, out _)).IsFalse();
  }

  [TestCase]
  public void OverlappingDebuffs_FollowConfiguredPriorityAndClearOnCleanse() {
    var (frame, hero) = CreateCharacter();
    var timers = new StatusEffectTimerController();
    timers.Enable(StatusEffectKind.Silence, "Silenced");
    timers.Enable(StatusEffectKind.Snare, "Snared");
    timers.EnableDebuff(StatType.Armor, "Weakened");
    timers.EnableDebuff(StatType.MoveSpeed, "Slowed");
    timers.Enable(StatusEffectKind.DamageOverTime, "Damage over time");
    SilenceController.Apply(ref frame, hero, 519, 20);
    SnareController.Apply(ref frame, hero, 511, 30);
    BuffsController.Apply(ref frame, hero, 515,
      [new BuffSpec(StatType.Armor, BuffMode.Flat, -FP64.One, FP64.Zero)], 1, 50);
    BuffsController.Apply(ref frame, hero, 505,
      [new BuffSpec(StatType.MoveSpeed, BuffMode.Flat, -FP64.One, FP64.Zero)], 1, 60);
    DamageOverTimeController.Apply(ref frame, hero, hero, 501, FP64.One, 80);
    AssertThat(timers.TryResolve(frame, hero, out var first)).IsTrue();
    AssertThat(first.Caption).IsEqual("Silenced");
    frame.Tick = 20;
    AssertThat(timers.TryResolve(frame, hero, out var second)).IsTrue();
    AssertThat(second.Caption).IsEqual("Snared");
    frame.Tick = 30;
    AssertThat(timers.TryResolve(frame, hero, out var third)).IsTrue();
    AssertThat(third.Caption).IsEqual("Weakened");
    frame.Tick = 50;
    AssertThat(timers.TryResolve(frame, hero, out var fourth)).IsTrue();
    AssertThat(fourth.Caption).IsEqual("Slowed");
    frame.Tick = 60;
    AssertThat(timers.TryResolve(frame, hero, out var fifth)).IsTrue();
    AssertThat(fifth.Caption).IsEqual("Damage over time");
    BuffsController.ClearNegative(ref frame, hero);
    AssertThat(timers.TryResolve(frame, hero, out _)).IsFalse();
  }

  [TestCase]
  public void OverlappingInstances_UseTheLongestRemainingEffect() {
    var (frame, hero) = CreateCharacter();
    var timers = new StatusEffectTimerController();
    timers.EnableDebuff(StatType.MoveSpeed, "Slowed");
    BuffsController.Apply(ref frame, hero, 501,
      [new BuffSpec(StatType.MoveSpeed, BuffMode.Flat, -FP64.One, FP64.Zero)], 1, 20);
    BuffsController.Apply(ref frame, hero, 505,
      [new BuffSpec(StatType.MoveSpeed, BuffMode.Flat, -FP64.One, FP64.Zero)], 1, 80);
    AssertThat(timers.TryResolve(frame, hero, out var full)).IsTrue();
    AssertThat(full.RemainingSeconds).IsEqual(4.0);
    frame.Tick = 40;
    AssertThat(timers.TryResolve(frame, hero, out var half)).IsTrue();
    AssertThat(half.RemainingFraction).IsEqual(0.5);
    AssertThat(half.RemainingSeconds).IsEqual(2.0);
  }

  [TestCase]
  public void Effects_AreDisabledUntilExplicitlyEnabled() {
    var (frame, hero) = CreateCharacter();
    SilenceController.Apply(ref frame, hero, 519, 80);
    SnareController.Apply(ref frame, hero, 511, 80);
    var timers = new StatusEffectTimerController();
    AssertThat(timers.TryResolve(frame, hero, out _)).IsFalse();
    timers.Enable(StatusEffectKind.Silence, "Silenced");
    AssertThat(timers.TryResolve(frame, hero, out var timer)).IsTrue();
    AssertThat(timer.Caption).IsEqual("Silenced");
    SilenceController.Clear(ref frame, hero);
    AssertThat(timers.TryResolve(frame, hero, out _)).IsFalse();
  }

  [TestCase]
  public void Silence_CountsDownFromStoredLifetime_AndDisappearsAtExpiry() {
    var (frame, hero) = CreateCharacter();
    var timers = new StatusEffectTimerController();
    timers.Enable(StatusEffectKind.Silence, "Silenced");
    frame.Tick = 10;
    SilenceController.Apply(ref frame, hero, 519, 80);
    AssertThat(timers.TryResolve(frame, hero, out var full)).IsTrue();
    AssertThat(full.RemainingFraction).IsEqual(1.0);
    AssertThat(full.RemainingSeconds).IsEqual(4.0);
    frame.Tick = 50;
    AssertThat(timers.TryResolve(frame, hero, out var half)).IsTrue();
    AssertThat(half.RemainingFraction).IsEqual(0.5);
    AssertThat(half.RemainingSeconds).IsEqual(2.0);
    frame.Tick = 90;
    AssertThat(timers.TryResolve(frame, hero, out _)).IsFalse();
  }

  [TestCase]
  public void ExtensionsUseNewLifetime_CleanseDeathAndOtherTargetsDoNotLeaveATimer() {
    var (frame, hero) = CreateCharacter();
    var timers = new StatusEffectTimerController();
    timers.Enable(StatusEffectKind.Silence, "Silenced");
    SilenceController.Apply(ref frame, hero, 519, 80);
    frame.Tick = 20;
    SilenceController.Apply(ref frame, hero, 519, 120);
    AssertThat(timers.TryResolve(frame, hero, out var extended)).IsTrue();
    AssertThat(extended.RemainingFraction).IsEqual(1.0);
    AssertThat(extended.RemainingSeconds).IsEqual(6.0);
    AssertThat(SilenceController.Apply(ref frame, hero, 519, 10)).IsFalse();
    frame.Tick = 80;
    AssertThat(timers.TryResolve(frame, hero, out var half)).IsTrue();
    AssertThat(half.RemainingFraction).IsEqual(0.5);
    var other = frame.CreateEntity();
    frame.Add(other, new UnitIdentity { UnitId = 2 });
    AssertThat(timers.TryResolve(frame, other, out _)).IsFalse();
    BuffsController.ClearNegative(ref frame, hero);
    AssertThat(timers.TryResolve(frame, hero, out _)).IsFalse();
    SilenceController.Apply(ref frame, hero, 519, 100);
    frame.Get<Health>(hero).Current = FP64.Zero;
    AssertThat(timers.TryResolve(frame, hero, out _)).IsFalse();
  }

  private static (Frame Frame, EntityRef Hero) CreateCharacter() {
    WarmupRegistry.RunAll();
    var simulation = new EcsSimulation(16, 2, deltaTimeMs: 50);
    simulation.Initialize();
    var frame = simulation.Frame;
    var hero = frame.CreateEntity();
    frame.Add(hero, new UnitIdentity { UnitId = 1 });
    frame.Add(hero, new Health(FP64.FromInt(100)));
    var stats = Stats.Create();
    stats.Set(StatType.MoveSpeed, FP64.FromInt(4));
    stats.Set(StatType.Armor, FP64.FromInt(30));
    stats.Set(StatType.MagicResist, FP64.FromInt(30));
    frame.Add(hero, stats);
    return (frame, hero);
  }
}
