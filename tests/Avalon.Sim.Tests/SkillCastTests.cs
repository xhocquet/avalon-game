using FluentAssertions;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using Meesles.Avalon.Sim.Heroes;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.ECS;
using Xunit;

namespace Meesles.Avalon.Sim.Tests;

public class SkillCastTests {
  private const int PlayerId = 1;
  private const int HardHit = (int)SkillSlot.HardHit;
  private const int Buff = (int)SkillSlot.Buff;

  [Fact]
  public void Cast_PutsTheSlotOnItsAuthoredCooldown() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var hero = harness.FindHero(PlayerId);
    var skill = SkillProgressionTests.SkillInSlot(harness, hero, SkillSlot.HardHit);
    var expectedTicks = SkillActions.CooldownTicks(ref frame, skill);

    LearnAndCast(harness);

    frame = harness.Frame;
    ref readonly var skills = ref frame.GetReadOnly<SkillsComponent>(harness.FindHero(PlayerId));
    // One tick already burned off: commands run before the Update phase, so SkillSystem decrements on
    // the same tick the cast landed.
    skills.GetCooldownRemainingTicks(HardHit).Should().Be(expectedTicks - 1);
  }

  [Fact]
  public void CooldownTicks_RoundUpFromTheAuthoredMilliseconds() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var hero = harness.FindHero(PlayerId);
    var skill = SkillProgressionTests.SkillInSlot(harness, hero, SkillSlot.HardHit);

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
    var remainingBefore = frame.GetReadOnly<SkillsComponent>(harness.FindHero(PlayerId))
      .GetCooldownRemainingTicks(HardHit);

    harness.Tick(SimHarness.CastSkillCommand(PlayerId, 2, HardHit));

    frame = harness.Frame;
    // Rejected, so the cooldown only advanced by the one tick that just ran - it was not restarted.
    frame.GetReadOnly<SkillsComponent>(harness.FindHero(PlayerId))
      .GetCooldownRemainingTicks(HardHit).Should().Be(remainingBefore - 1);
  }

  [Fact]
  public void Cast_IsAvailableAgainOnceTheCooldownExpires() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var hero = harness.FindHero(PlayerId);
    var skill = SkillProgressionTests.SkillInSlot(harness, hero, SkillSlot.HardHit);
    var cooldownTicks = SkillActions.CooldownTicks(ref frame, skill);

    LearnAndCast(harness);
    for (var i = 0; i < cooldownTicks; i++)
      harness.Tick();

    frame = harness.Frame;
    frame.GetReadOnly<SkillsComponent>(harness.FindHero(PlayerId)).IsReady(HardHit).Should().BeTrue();

    harness.Tick(SimHarness.CastSkillCommand(PlayerId, 100, HardHit));

    frame = harness.Frame;
    frame.GetReadOnly<SkillsComponent>(harness.FindHero(PlayerId))
      .GetCooldownRemainingTicks(HardHit).Should().Be(cooldownTicks - 1);
  }

  [Fact]
  public void Cast_OfAnUnlearnedSlot_IsRejected() {
    var harness = SimHarness.CreateInitialized();

    harness.Tick(SimHarness.CastSkillCommand(PlayerId, 0, HardHit));

    var frame = harness.Frame;
    ref readonly var skills = ref frame.GetReadOnly<SkillsComponent>(harness.FindHero(PlayerId));
    skills.GetRank(HardHit).Should().Be(0);
    skills.GetCooldownRemainingTicks(HardHit).Should().Be(0);
  }

  [Fact]
  public void Cast_WhileDead_IsRejected() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var hero = harness.FindHero(PlayerId);
    frame.Get<SkillsComponent>(hero).TrySpendPoint(HardHit, 4).Should().BeTrue();
    frame.Get<Health>(hero).Current = 0;

    SkillActions.TryCast(ref frame, PlayerId, HardHit).Should().BeFalse();

    frame.GetReadOnly<SkillsComponent>(hero).GetCooldownRemainingTicks(HardHit).Should().Be(0);
  }

  [Fact]
  public void Cast_OnlyCoolsDownTheSlotItWasCastFrom() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var hero = harness.FindHero(PlayerId);
    SkillProgressionTests.GrantPoints(ref frame, hero, 2);
    harness.Tick(SimHarness.UpgradeSkillCommand(PlayerId, 0, HardHit));
    harness.Tick(SimHarness.UpgradeSkillCommand(PlayerId, 1, Buff));

    harness.Tick(SimHarness.CastSkillCommand(PlayerId, 2, HardHit));

    frame = harness.Frame;
    ref readonly var skills = ref frame.GetReadOnly<SkillsComponent>(harness.FindHero(PlayerId));
    skills.GetCooldownRemainingTicks(HardHit).Should().BeGreaterThan(0);
    skills.GetCooldownRemainingTicks(Buff).Should().Be(0);
    skills.IsReady(Buff).Should().BeTrue();
  }

  [Fact]
  public void Cast_OnOneHero_LeavesTheOtherHerosCooldownsAlone() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    frame.Get<SkillsComponent>(harness.FindHero(2)).TrySpendPoint(HardHit, 4).Should().BeTrue();

    LearnAndCast(harness);

    frame = harness.Frame;
    ref readonly var other = ref frame.GetReadOnly<SkillsComponent>(harness.FindHero(2));
    other.GetCooldownRemainingTicks(HardHit).Should().Be(0);
    other.IsReady(HardHit).Should().BeTrue();
  }

  [Fact]
  public void Cast_RaisesSkillCastEventWithThePayloadTheViewNeeds() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var hero = harness.FindHero(PlayerId);
    var unitId = frame.GetReadOnly<UnitIdComponent>(hero).UnitId;
    var position = frame.GetReadOnly<TransformComponent>(hero).Position;
    frame.Get<SkillsComponent>(hero).TrySpendPoint(Buff, 4).Should().BeTrue();
    var expectedSkillId = frame.GetReadOnly<SkillsComponent>(hero).GetSkillAssetId(Buff);

    var collector = new EventCollector();
    collector.BeginTick(12);
    frame.EventRaiser = collector;

    SkillActions.TryCast(ref frame, PlayerId, Buff).Should().BeTrue();

    var evt = collector.Collected[0].Should().BeOfType<SkillCastEvent>().Subject;
    evt.Tick.Should().Be(12);
    evt.UnitId.Should().Be(unitId);
    evt.PlayerId.Should().Be(PlayerId);
    evt.Slot.Should().Be(Buff);
    evt.SkillAssetId.Should().Be(expectedSkillId);
    evt.Rank.Should().Be(1);
    evt.Position.Should().Be(position);
  }

  [Theory]
  [InlineData(-1)]
  [InlineData(SkillsComponent.MaxSlots)]
  public void Cast_WithAnOutOfRangeSlot_IsRejectedBeforeTouchingTheBuffers(int slot) {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var hero = harness.FindHero(PlayerId);
    for (var i = 0; i < SkillsComponent.MaxSlots; i++)
      frame.Get<SkillsComponent>(hero).SetSkillAssetId(i, frame.GetReadOnly<SkillsComponent>(hero).GetSkillAssetId(i));

    harness.Tick(SimHarness.CastSkillCommand(PlayerId, 0, slot));

    frame = harness.Frame;
    ref readonly var skills = ref frame.GetReadOnly<SkillsComponent>(harness.FindHero(PlayerId));
    for (var i = 0; i < SkillsComponent.MaxSlots; i++)
      skills.GetCooldownRemainingTicks(i).Should().Be(0);
  }

  [Fact]
  public void SkillSystem_NeverDrivesACooldownNegative() {
    var harness = SimHarness.CreateInitialized();
    LearnAndCast(harness);

    for (var i = 0; i < 600; i++)
      harness.Tick();

    var frame = harness.Frame;
    ref readonly var skills = ref frame.GetReadOnly<SkillsComponent>(harness.FindHero(PlayerId));
    for (var i = 0; i < SkillsComponent.MaxSlots; i++)
      skills.GetCooldownRemainingTicks(i).Should().Be(0);
  }

  // Rank up HardHit and cast it, leaving the sim one tick past the cast.
  private static void LearnAndCast(SimHarness harness) {
    harness.Tick(SimHarness.UpgradeSkillCommand(PlayerId, 0, HardHit));
    harness.Tick(SimHarness.CastSkillCommand(PlayerId, 1, HardHit));
  }
}
