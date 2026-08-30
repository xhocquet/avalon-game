using System.Linq;
using FluentAssertions;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using Meesles.Avalon.Sim.Heroes;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using Xunit;

namespace Meesles.Avalon.Sim.Tests;

public class SkillCastTests {
  private const int PlayerId = 1;
  private const int Primary = (int)SkillSlot.Primary;
  private const int Secondary = (int)SkillSlot.Secondary;

  [Fact]
  public void Cast_PutsTheSlotOnItsAuthoredCooldown() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var hero = harness.FindHero(PlayerId);
    var skill = SkillProgressionTests.SkillInSlot(harness, hero, SkillSlot.Primary);
    var expectedTicks = SkillActions.CooldownTicks(ref frame, skill);

    LearnAndCast(harness);

    frame = harness.Frame;
    ref readonly var skills = ref frame.GetReadOnly<Skills>(harness.FindHero(PlayerId));
    // One tick already burned off: commands run before the Update phase, so the cooldown pass decrements on
    // the same tick the cast landed.
    skills.GetCooldownRemainingTicks(Primary).Should().Be(expectedTicks - 1);
  }

  [Fact]
  public void CooldownTicks_RoundUpFromTheAuthoredMilliseconds() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var hero = harness.FindHero(PlayerId);
    var skill = SkillProgressionTests.SkillInSlot(harness, hero, SkillSlot.Primary);

    var ticks = SkillActions.CooldownTicks(ref frame, skill);

    var deltaTimeMs = frame.DeltaTimeMs > 0 ? frame.DeltaTimeMs : 16;
    ticks.Should().Be((skill.CooldownMs + deltaTimeMs - 1) / deltaTimeMs);
    // Rounding up, so the cooldown is never shorter than the authored 3 seconds.
    (ticks * deltaTimeMs).Should().BeGreaterThanOrEqualTo(skill.CooldownMs);
  }

  [Fact]
  public void Cast_WhileOnCooldown_IsRejected() {
    var harness = SimHarness.CreateInitialized();
    LearnAndCast(harness);

    var frame = harness.Frame;
    var remainingBefore = frame.GetReadOnly<Skills>(harness.FindHero(PlayerId))
      .GetCooldownRemainingTicks(Primary);

    harness.Tick(SimHarness.CastSkillCommand(PlayerId, 2, Primary));

    frame = harness.Frame;
    // Rejected, so the cooldown only advanced by the one tick that just ran - it was not restarted.
    frame.GetReadOnly<Skills>(harness.FindHero(PlayerId))
      .GetCooldownRemainingTicks(Primary).Should().Be(remainingBefore - 1);
  }

  [Fact]
  public void Cast_IsAvailableAgainOnceTheCooldownExpires() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var hero = harness.FindHero(PlayerId);
    var skill = SkillProgressionTests.SkillInSlot(harness, hero, SkillSlot.Primary);
    var cooldownTicks = SkillActions.CooldownTicks(ref frame, skill);

    LearnAndCast(harness);
    for (var i = 0; i < cooldownTicks; i++)
      harness.Tick();

    frame = harness.Frame;
    frame.GetReadOnly<Skills>(harness.FindHero(PlayerId)).IsReady(Primary).Should().BeTrue();

    harness.Tick(SimHarness.CastSkillCommand(PlayerId, 100, Primary));

    frame = harness.Frame;
    frame.GetReadOnly<Skills>(harness.FindHero(PlayerId))
      .GetCooldownRemainingTicks(Primary).Should().Be(cooldownTicks - 1);
  }

  [Fact]
  public void Cast_OfAnUnlearnedSlot_IsRejected() {
    var harness = SimHarness.CreateInitialized();

    harness.Tick(SimHarness.CastSkillCommand(PlayerId, 0, Primary));

    var frame = harness.Frame;
    ref readonly var skills = ref frame.GetReadOnly<Skills>(harness.FindHero(PlayerId));
    skills.GetRank(Primary).Should().Be(0);
    skills.GetCooldownRemainingTicks(Primary).Should().Be(0);
  }

  [Fact]
  public void Cast_WhileDead_IsRejected() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var hero = harness.FindHero(PlayerId);
    frame.Get<Skills>(hero).TrySpendPoint(Primary, 4).Should().BeTrue();
    frame.Get<Health>(hero).Current = 0;

    SkillActions.TryCast(ref frame, PlayerId, Primary, FPVector3.Zero).Should().BeFalse();

    frame.GetReadOnly<Skills>(hero).GetCooldownRemainingTicks(Primary).Should().Be(0);
  }

  [Fact]
  public void Cast_OnlyCoolsDownTheSlotItWasCastFrom() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var hero = harness.FindHero(PlayerId);
    SkillProgressionTests.GrantPoints(ref frame, hero, 2);
    harness.Tick(SimHarness.UpgradeSkillCommand(PlayerId, 0, Primary));
    harness.Tick(SimHarness.UpgradeSkillCommand(PlayerId, 1, Secondary));

    harness.Tick(SimHarness.CastSkillCommand(PlayerId, 2, Primary));

    frame = harness.Frame;
    ref readonly var skills = ref frame.GetReadOnly<Skills>(harness.FindHero(PlayerId));
    skills.GetCooldownRemainingTicks(Primary).Should().BeGreaterThan(0);
    skills.GetCooldownRemainingTicks(Secondary).Should().Be(0);
    skills.IsReady(Secondary).Should().BeTrue();
  }

  [Fact]
  public void Cast_OnOneHero_LeavesTheOtherHerosCooldownsAlone() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    frame.Get<Skills>(harness.FindHero(2)).TrySpendPoint(Primary, 4).Should().BeTrue();

    LearnAndCast(harness);

    frame = harness.Frame;
    ref readonly var other = ref frame.GetReadOnly<Skills>(harness.FindHero(2));
    other.GetCooldownRemainingTicks(Primary).Should().Be(0);
    other.IsReady(Primary).Should().BeTrue();
  }

  [Fact]
  public void Cast_RaisesSkillCastEventWithThePayloadTheViewNeeds() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var hero = harness.FindHero(PlayerId);
    var unitId = frame.GetReadOnly<UnitIdentity>(hero).UnitId;
    var position = frame.GetReadOnly<TransformComponent>(hero).Position;
    frame.Get<Skills>(hero).TrySpendPoint(Secondary, 4).Should().BeTrue();
    var expectedSkillId = frame.GetReadOnly<Skills>(hero).GetSkillAssetId(Secondary);

    var collector = new EventCollector();
    collector.BeginTick(12);
    frame.EventRaiser = collector;

    var target = position + new FPVector3(FP64.FromInt(5), FP64.Zero, FP64.FromInt(-3));
    SkillActions.TryCast(ref frame, PlayerId, Secondary, target).Should().BeTrue();

    // Strangle also spawns a bolt, so the cast event is not necessarily first; the view keys off the type.
    var evt = collector.Collected.OfType<SkillCastEvent>().Single();
    evt.Tick.Should().Be(12);
    evt.UnitId.Should().Be(unitId);
    evt.PlayerId.Should().Be(PlayerId);
    evt.Slot.Should().Be(Secondary);
    evt.SkillAssetId.Should().Be(expectedSkillId);
    evt.Rank.Should().Be(1);
    evt.Position.Should().Be(position);
    // Flattened to the ground plane on the way in, so the view never gets a target off the map floor.
    evt.TargetPosition.Should().Be(new FPVector3(target.x, FP64.Zero, target.z));
  }

  [Theory]
  [InlineData(-1)]
  [InlineData(Skills.MaxSlots)]
  public void Cast_WithAnOutOfRangeSlot_IsRejectedBeforeTouchingTheBuffers(int slot) {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var hero = harness.FindHero(PlayerId);
    for (var i = 0; i < Skills.MaxSlots; i++)
      frame.Get<Skills>(hero).SetSkillAssetId(i, frame.GetReadOnly<Skills>(hero).GetSkillAssetId(i));

    harness.Tick(SimHarness.CastSkillCommand(PlayerId, 0, slot));

    frame = harness.Frame;
    ref readonly var skills = ref frame.GetReadOnly<Skills>(harness.FindHero(PlayerId));
    for (var i = 0; i < Skills.MaxSlots; i++)
      skills.GetCooldownRemainingTicks(i).Should().Be(0);
  }

  [Fact]
  public void SkillCooldown_IsNeverDrivenNegative() {
    var harness = SimHarness.CreateInitialized();
    LearnAndCast(harness);

    for (var i = 0; i < 600; i++)
      harness.Tick();

    var frame = harness.Frame;
    ref readonly var skills = ref frame.GetReadOnly<Skills>(harness.FindHero(PlayerId));
    for (var i = 0; i < Skills.MaxSlots; i++)
      skills.GetCooldownRemainingTicks(i).Should().Be(0);
  }

  [Fact]
  public void Cast_SpendsTheAuthoredManaCost() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var hero = harness.FindHero(PlayerId);
    SkillProgressionTests.SkillInSlot(harness, hero, SkillSlot.Primary).ManaCost = FP64.FromInt(100);
    var manaBefore = frame.GetReadOnly<Health>(hero).Mana;

    LearnAndCast(harness);

    frame = harness.Frame;
    frame.GetReadOnly<Health>(harness.FindHero(PlayerId)).Mana.Should().Be(manaBefore - FP64.FromInt(100));
  }

  [Fact]
  public void Cast_WithoutEnoughMana_IsRejected() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var hero = harness.FindHero(PlayerId);
    SkillProgressionTests.SkillInSlot(harness, hero, SkillSlot.Primary).ManaCost = FP64.FromInt(100);
    frame.Get<Skills>(hero).TrySpendPoint(Primary, 4).Should().BeTrue();
    frame.Get<Health>(hero).Mana = FP64.FromInt(40);

    SkillActions.TryCast(ref frame, PlayerId, Primary, FPVector3.Zero).Should().BeFalse();

    frame.GetReadOnly<Skills>(hero).GetCooldownRemainingTicks(Primary).Should().Be(0);
    frame.GetReadOnly<Health>(hero).Mana.Should().Be(FP64.FromInt(40)); // nothing spent on a rejected cast
  }

  [Fact]
  public void CanCast_IsFalseWhenThePoolCannotCoverTheCost() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var hero = harness.FindHero(PlayerId);
    SkillProgressionTests.SkillInSlot(harness, hero, SkillSlot.Primary).ManaCost = FP64.FromInt(100);
    frame.Get<Skills>(hero).TrySpendPoint(Primary, 4).Should().BeTrue();

    SkillActions.CanCast(ref frame, PlayerId, Primary).Should().BeTrue();
    frame.Get<Health>(hero).Mana = FP64.FromInt(40);
    SkillActions.CanCast(ref frame, PlayerId, Primary).Should().BeFalse();
  }

  // Rank up Primary and cast it, leaving the sim one tick past the cast.
  private static void LearnAndCast(SimHarness harness) {
    harness.Tick(SimHarness.UpgradeSkillCommand(PlayerId, 0, Primary));
    harness.Tick(SimHarness.CastSkillCommand(PlayerId, 1, Primary));
  }
}
