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
public class CharacterNameMessagesTests {
  [TestCase("Armor flat 10; MagicResist flat 10", "Defense Up!")]
  [TestCase("MoveSpeed pct 0.2", "Speed Up!")]
  [TestCase("AttackDamage flat 10", "Damage Up!")]
  [TestCase("BonusAttackSpeed flat 0.2", "Faster Attacks!")]
  [TestCase("BaseAttackSpeed flat 0.2", "Faster Attacks!")]
  public void PositiveStatEffects_UseTheirBuffLabels(string buffStats, string expected) {
    var (frame, hero) = CreateCharacter();
    var skill = new SkillAsset(500) { BuffDurationMs = 60000, BuffStats = buffStats };
    AssertThat(BuffsController.ApplySkill(ref frame, hero, skill, 1)).IsTrue();
    var messages = new CharacterNameMessages();
    AssertThat(messages.Resolve(frame, hero, false, "Name", 0)).IsEqual(expected);
    AssertThat(messages.Resolve(frame, hero, false, "Name", 1)).IsEqual(expected);
  }

  [TestCase]
  public void EmpoweredAttack_UsesDamageLabelUntilConsumed() {
    var (frame, hero) = CreateCharacter();
    var messages = new CharacterNameMessages();
    EmpoweredAttackController.Arm(ref frame, hero, 508, FP64.FromInt(2), 1000);
    AssertThat(messages.Resolve(frame, hero, false, "Name", 0)).IsEqual("Damage Up!");
    frame.Get<AttackProc>(hero).Clear();
    AssertThat(messages.Resolve(frame, hero, false, "Name", 0.25)).IsEqual("Name");
  }

  [TestCase]
  public void Cleanse_OverridesMessagesForExactlyOneSecondThenResumesRotation() {
    var (frame, hero) = CreateCharacter();
    var messages = new CharacterNameMessages();
    var skill = new SkillAsset(500) {
      BuffDurationMs = 60000, BuffStats = "Armor flat 10; MagicResist flat 10; MoveSpeed pct 0.2"
    };
    AssertThat(BuffsController.ApplySkill(ref frame, hero, skill, 1)).IsTrue();
    AssertThat(messages.Resolve(frame, hero, true, "Name", 0)).IsEqual("Speed Up!");
    AssertThat(messages.Resolve(frame, hero, true, "Name", 0.5, 1.5)).IsEqual("Cleansed!");
    AssertThat(messages.Resolve(frame, hero, true, "Name", 1.49, 1.5)).IsEqual("Cleansed!");
    AssertThat(messages.Resolve(frame, hero, true, "Name", 1.5)).IsEqual("Speed Up!");
    AssertThat(messages.Resolve(frame, hero, true, "Name", 2.5)).IsEqual("Defense Up!");
    BuffsController.ClearAll(ref frame, hero);
    AssertThat(messages.Resolve(frame, hero, true, "Name", 3, 4)).IsEqual("Cleansed!");
    AssertThat(messages.Resolve(frame, hero, true, "Name", 4)).IsEqual("Upgrade skill!");
    AssertThat(messages.Resolve(frame, hero, false, "Name", 4)).IsEqual("Name");
  }

  [TestCase]
  public void SnaresAndWeakened_RotateWithoutDotTextOrDuplicateResistanceLabels() {
    var (frame, hero) = CreateCharacter();
    var messages = new CharacterNameMessages();
    AssertThat(DamageOverTimeController.Apply(ref frame, hero, hero, 501, FP64.One, 1000)).IsTrue();
    AssertThat(messages.Resolve(frame, hero, false, "Name", 0)).IsEqual("Name");
    SnareController.Apply(ref frame, hero, 511, 1000);
    AssertThat(messages.Resolve(frame, hero, false, "Name", 0.25)).IsEqual("Snared!");
    frame.Tick++;
    var weakened = new SkillAsset(515) {
      BuffDurationMs = 60000,
      BuffStats = "Armor flat -10; MagicResist flat -10"
    };
    AssertThat(BuffsController.ApplySkill(ref frame, hero, weakened, 1)).IsTrue();
    AssertThat(messages.Resolve(frame, hero, false, "Name", 0.5)).IsEqual("Weakened!");
    AssertThat(messages.Resolve(frame, hero, false, "Name", 1.5)).IsEqual("Snared!");
    frame.Tick++;
    AssertThat(DamageOverTimeController.Apply(ref frame, hero, hero, 503, FP64.One, 1000)).IsTrue();
    AssertThat(messages.Resolve(frame, hero, false, "Name", 1.75)).IsEqual("Snared!");
    AssertThat(messages.Resolve(frame, hero, false, "Name", 2.5)).IsEqual("Weakened!");
    SnareController.Clear(ref frame, hero);
    AssertThat(messages.Resolve(frame, hero, false, "Name", 3.5)).IsEqual("Weakened!");
    BuffsController.ClearNegative(ref frame, hero);
    AssertThat(messages.Resolve(frame, hero, false, "Name", 3.6)).IsEqual("Name");
  }

  [TestCase]
  public void MixedStatEffect_CanSupplyBothSlowAndWeakenedMessages() {
    var (frame, hero) = CreateCharacter();
    var messages = new CharacterNameMessages();
    var skill = new SkillAsset(515) {
      BuffDurationMs = 60000, BuffStats = "MoveSpeed pct -0.3; MagicResist flat -10; AttackDamage flat 10"
    };
    AssertThat(BuffsController.ApplySkill(ref frame, hero, skill, 1)).IsTrue();
    AssertThat(messages.Resolve(frame, hero, false, "Name", 0)).IsEqual("Damage Up!");
    AssertThat(messages.Resolve(frame, hero, false, "Name", 1)).IsEqual("Slowed!");
    AssertThat(messages.Resolve(frame, hero, false, "Name", 2)).IsEqual("Weakened!");
    AssertThat(messages.Resolve(frame, hero, false, "Name", 3)).IsEqual("Damage Up!");
  }

  [TestCase]
  public void NewDebuffs_InterruptAndRestartOneSecondRotation() {
    var (frame, hero) = CreateCharacter();
    var messages = new CharacterNameMessages();
    AssertThat(messages.Resolve(frame, hero, true, "Name", 0)).IsEqual("Upgrade skill!");
    SilenceController.Apply(ref frame, hero, 519, 1000);
    AssertThat(messages.Resolve(frame, hero, true, "Name", 0)).IsEqual("Silenced!");
    frame.Tick++;
    Slow(ref frame, hero, 505);
    AssertThat(messages.Resolve(frame, hero, true, "Name", 0.25)).IsEqual("Slowed!");
    AssertThat(messages.Resolve(frame, hero, true, "Name", 1.24)).IsEqual("Slowed!");
    AssertThat(messages.Resolve(frame, hero, true, "Name", 1.25)).IsEqual("Silenced!");
    AssertThat(messages.Resolve(frame, hero, true, "Name", 2.25)).IsEqual("Slowed!");
    AssertThat(messages.Resolve(frame, hero, true, "Name", 3.25)).IsEqual("Silenced!");
    frame.Tick++;
    Slow(ref frame, hero, 501);
    AssertThat(messages.Resolve(frame, hero, true, "Name", 3.5)).IsEqual("Slowed!");
    AssertThat(messages.Resolve(frame, hero, true, "Name", 4.49)).IsEqual("Slowed!");
    AssertThat(messages.Resolve(frame, hero, true, "Name", 4.5)).IsEqual("Silenced!");
    AssertThat(messages.Resolve(frame, hero, true, "Name", 5.5)).IsEqual("Slowed!");
    BuffsController.ClearNegative(ref frame, hero);
    AssertThat(messages.Resolve(frame, hero, true, "Name", 5.6)).IsEqual("Upgrade skill!");
    AssertThat(messages.Resolve(frame, hero, false, "Name", 5.6)).IsEqual("Name");
  }

  [TestCase]
  public void Refreshes_DoNotStarveRotation_ExpiredEffectsDisappear() {
    var (frame, hero) = CreateCharacter();
    var messages = new CharacterNameMessages();
    Slow(ref frame, hero, 505);
    AssertThat(messages.Resolve(frame, hero, false, "Name", 0)).IsEqual("Slowed!");
    frame.Tick++;
    SilenceController.Apply(ref frame, hero, 519, 10);
    AssertThat(messages.Resolve(frame, hero, false, "Name", 0.25)).IsEqual("Silenced!");
    frame.Tick++;
    Slow(ref frame, hero, 505);
    AssertThat(messages.Resolve(frame, hero, false, "Name", 0.5)).IsEqual("Silenced!");
    AssertThat(messages.Resolve(frame, hero, false, "Name", 1.25)).IsEqual("Slowed!");
    frame.Tick = 11;
    AssertThat(messages.Resolve(frame, hero, false, "Name", 2.25)).IsEqual("Slowed!");
    frame.Tick = 2000;
    AssertThat(messages.Resolve(frame, hero, false, "Name", 3.25)).IsEqual("Name");
  }

  [TestCase]
  public void ResetAndCharacterChanges_DoNotKeepPreviousRotation() {
    var (frame, hero) = CreateCharacter();
    var messages = new CharacterNameMessages();
    SilenceController.Apply(ref frame, hero, 519, 1000);
    messages.Resolve(frame, hero, false, "Name", 0);
    frame.Tick++;
    Slow(ref frame, hero, 505);
    messages.Resolve(frame, hero, false, "Name", 0.25);
    messages.Reset();
    AssertThat(messages.Resolve(frame, hero, false, "Name", 5)).IsEqual("Slowed!");
    AssertThat(messages.Resolve(frame, hero, false, "Name", 5.99)).IsEqual("Slowed!");
    AssertThat(messages.Resolve(frame, hero, false, "Name", 6)).IsEqual("Silenced!");
    var other = frame.CreateEntity();
    frame.Add(other, new UnitIdentity { UnitId = 2 });
    AssertThat(messages.Resolve(frame, other, false, "Other", 6.1)).IsEqual("Other");
  }

  private static (Frame Frame, EntityRef Hero) CreateCharacter() {
    WarmupRegistry.RunAll();
    var simulation = new EcsSimulation(16, 2);
    simulation.Initialize();
    var frame = simulation.Frame;
    var hero = frame.CreateEntity();
    frame.Add(hero, new UnitIdentity { UnitId = 1 });
    frame.Add(hero, new Health(FP64.FromInt(100)));
    var stats = Stats.Create();
    stats.Set(StatType.MoveSpeed, FP64.FromInt(4));
    frame.Add(hero, stats);
    return (frame, hero);
  }

  private static void Slow(ref Frame frame, EntityRef hero, int skillId) {
    var skill = new SkillAsset(skillId) { BuffDurationMs = 60000, BuffStats = "MoveSpeed pct -0.3" };
    AssertThat(BuffsController.ApplySkill(ref frame, hero, skill, 1)).IsTrue();
  }
}
