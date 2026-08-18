using System.Collections.Generic;
using FluentAssertions;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Commands;
using Meesles.Avalon.Sim.Components;
using Meesles.Avalon.Sim.Heroes;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Serialization;
using Xunit;

namespace Meesles.Avalon.Sim.Tests;

// Earning and spending skill points. Casting lives in SkillCastTests.
public class SkillProgressionTests {
  private const int PlayerId = 1;

  [Fact]
  public void SpawnedHero_HasOnePointAndItsOwnFourSkillsUnranked() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var hero = harness.FindHero(PlayerId);
    var heroAsset = harness.AssetRegistry.Get<HeroAsset>(frame.GetReadOnly<Hero>(hero).HeroAssetId);

    ref readonly var skills = ref frame.GetReadOnly<SkillsComponent>(hero);
    skills.SkillPoints.Should().Be(1, "level 1 counts as a level, so one pick is available at spawn");
    for (var slot = 0; slot < SkillsComponent.MaxSlots; slot++) {
      skills.GetSkillAssetId(slot).Should().Be(heroAsset.GetSkillAssetId(slot));
      skills.GetRank(slot).Should().Be(0);
      skills.GetCooldownRemainingTicks(slot).Should().Be(0);
    }
  }

  [Fact]
  public void EveryLevelGained_GrantsExactlyOnePoint() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var rules = harness.AssetRegistry.Get<XpRulesAsset>();
    var hero = harness.FindHero(PlayerId);
    frame.Get<ExperienceComponent>(hero).Experience = rules.TotalXpForLevel(4);

    new ExperienceSystem().Update(ref frame);

    // Three levels gained on top of the point the hero spawned with.
    frame.GetReadOnly<ExperienceComponent>(hero).Level.Should().Be(4);
    frame.GetReadOnly<SkillsComponent>(hero).SkillPoints.Should().Be(4);
  }

  [Fact]
  public void PointsTrackLevel_AcrossSeparateLevelUps() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var rules = harness.AssetRegistry.Get<XpRulesAsset>();
    var hero = harness.FindHero(PlayerId);
    var system = new ExperienceSystem();

    for (var level = 2; level <= 5; level++) {
      frame.Get<ExperienceComponent>(hero).Experience = rules.TotalXpForLevel(level);
      system.Update(ref frame);
      frame.GetReadOnly<SkillsComponent>(hero).SkillPoints.Should().Be(level);
    }
  }

  [Fact]
  public void Upgrade_SpendsOnePointAndRaisesTheSlotOneRank() {
    var harness = SimHarness.CreateInitialized();

    harness.Tick(SimHarness.UpgradeSkillCommand(PlayerId, 0, (int)SkillSlot.Primary));

    var frame = harness.Frame;
    ref readonly var skills = ref frame.GetReadOnly<SkillsComponent>(harness.FindHero(PlayerId));
    skills.SkillPoints.Should().Be(0);
    skills.GetRank((int)SkillSlot.Primary).Should().Be(1);
    skills.GetRank((int)SkillSlot.Secondary).Should().Be(0);
  }

  [Fact]
  public void Upgrade_WithNoPointsLeft_IsRejected() {
    var harness = SimHarness.CreateInitialized();
    harness.Tick(SimHarness.UpgradeSkillCommand(PlayerId, 0, (int)SkillSlot.Primary));

    harness.Tick(SimHarness.UpgradeSkillCommand(PlayerId, 1, (int)SkillSlot.Secondary));

    var frame = harness.Frame;
    ref readonly var skills = ref frame.GetReadOnly<SkillsComponent>(harness.FindHero(PlayerId));
    skills.SkillPoints.Should().Be(0);
    skills.GetRank((int)SkillSlot.Secondary).Should().Be(0);
  }

  [Fact]
  public void Upgrade_StopsAtMaxRank_LeavingThePointUnspent() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var hero = harness.FindHero(PlayerId);
    var skill = SkillInSlot(harness, hero, SkillSlot.Primary);
    GrantPoints(ref frame, hero, skill.MaxRank + 3);

    for (var i = 0; i < skill.MaxRank + 2; i++)
      harness.Tick(SimHarness.UpgradeSkillCommand(PlayerId, i, (int)SkillSlot.Primary));

    frame = harness.Frame;
    ref readonly var skills = ref frame.GetReadOnly<SkillsComponent>(harness.FindHero(PlayerId));
    skills.GetRank((int)SkillSlot.Primary).Should().Be(skill.MaxRank);
    // Only MaxRank of the granted points could be spent; the surplus stays banked.
    skills.SkillPoints.Should().Be(3);
  }

  [Theory]
  [InlineData(-1)]
  [InlineData(SkillsComponent.MaxSlots)]
  [InlineData(9999)]
  public void Upgrade_WithAnOutOfRangeSlot_IsRejectedWithoutSpending(int slot) {
    var harness = SimHarness.CreateInitialized();

    harness.Tick(SimHarness.UpgradeSkillCommand(PlayerId, 0, slot));

    var frame = harness.Frame;
    ref readonly var skills = ref frame.GetReadOnly<SkillsComponent>(harness.FindHero(PlayerId));
    skills.SkillPoints.Should().Be(1);
    for (var i = 0; i < SkillsComponent.MaxSlots; i++)
      skills.GetRank(i).Should().Be(0);
  }

  [Fact]
  public void Upgrade_RaisesSkillUpgradedEventWithThePayloadTheViewNeeds() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var hero = harness.FindHero(PlayerId);
    var unitId = frame.GetReadOnly<UnitIdComponent>(hero).UnitId;
    var expectedSkillId = frame.GetReadOnly<SkillsComponent>(hero).GetSkillAssetId((int)SkillSlot.Ultimate);

    var collector = new EventCollector();
    collector.BeginTick(7);
    frame.EventRaiser = collector;

    SkillActions.TryUpgrade(ref frame, PlayerId, (int)SkillSlot.Ultimate).Should().BeTrue();

    var evt = collector.Collected[0].Should().BeOfType<SkillUpgradedEvent>().Subject;
    evt.Tick.Should().Be(7);
    evt.UnitId.Should().Be(unitId);
    evt.PlayerId.Should().Be(PlayerId);
    evt.Slot.Should().Be((int)SkillSlot.Ultimate);
    evt.SkillAssetId.Should().Be(expectedSkillId);
    evt.NewRank.Should().Be(1);
    evt.RemainingPoints.Should().Be(0);
  }

  [Fact]
  public void Upgrade_OnOneHero_LeavesTheOtherHeroAlone() {
    var harness = SimHarness.CreateInitialized();

    harness.Tick(SimHarness.UpgradeSkillCommand(PlayerId, 0, (int)SkillSlot.Primary));

    var frame = harness.Frame;
    ref readonly var other = ref frame.GetReadOnly<SkillsComponent>(harness.FindHero(2));
    other.SkillPoints.Should().Be(1);
    other.GetRank((int)SkillSlot.Primary).Should().Be(0);
  }

  [Fact]
  public void UpgradeSkillCommand_RoundTripsThroughTheWire() {
    var original = new UpgradeSkillCommand { PlayerId = 2, Tick = 9, Slot = 3 };
    original.GetSerializedSize().Should().Be(16);

    var buffer = new byte[original.GetSerializedSize()];
    var writer = new SpanWriter(buffer);
    original.Serialize(ref writer);

    var restored = new UpgradeSkillCommand();
    var reader = new SpanReader(buffer);
    restored.Deserialize(ref reader);

    restored.PlayerId.Should().Be(2);
    restored.Tick.Should().Be(9);
    restored.Slot.Should().Be(3);
  }

  [Fact]
  public void CastSkillCommand_RoundTripsThroughTheWire() {
    var original = new CastSkillCommand {
      PlayerId = 4,
      Tick = 21,
      Slot = 2,
      TargetX = FP64.FromFloat(12.5f),
      TargetZ = FP64.FromFloat(-7.25f)
    };
    original.GetSerializedSize().Should().Be(32);

    var buffer = new byte[original.GetSerializedSize()];
    var writer = new SpanWriter(buffer);
    original.Serialize(ref writer);

    var restored = new CastSkillCommand();
    var reader = new SpanReader(buffer);
    restored.Deserialize(ref reader);

    restored.PlayerId.Should().Be(4);
    restored.Tick.Should().Be(21);
    restored.Slot.Should().Be(2);
    restored.TargetX.Should().Be(original.TargetX);
    restored.TargetZ.Should().Be(original.TargetZ);
  }

  // Catches drift between AssetIds, the hero asset files, and HeroSkillSets at test time rather than at spawn,
  // where a missing row would only surface as a rejected upgrade or a thrown KeyNotFoundException.
  [Fact]
  public void EveryHeroRow_NamesFourLoadableSkillsAndARegisteredSkillSet() {
    var harness = SimHarness.CreateInitialized();
    int[] heroAssetIds = [
      AssetIds.HeroHairyWizard, AssetIds.HeroShroom, AssetIds.HeroCrystalGiant,
      AssetIds.HeroSkinwalker, AssetIds.HeroPickleKnight
    ];

    foreach (var heroAssetId in heroAssetIds) {
      var heroAsset = harness.AssetRegistry.Get<HeroAsset>(heroAssetId);
      heroAsset.Should().NotBeNull($"hero {heroAssetId} must exist");
      HeroSkillSets.Get(heroAsset.SkillSetId).Should().NotBeNull();

      for (var slot = 0; slot < SkillsComponent.MaxSlots; slot++) {
        var skillAssetId = heroAsset.GetSkillAssetId(slot);
        skillAssetId.Should().NotBe(0, $"hero {heroAssetId} slot {slot} must name a skill");

        var skill = harness.AssetRegistry.Get<SkillAsset>(skillAssetId);
        skill.Should().NotBeNull($"skill {skillAssetId} must load from the .bytes");
        skill.MaxRank.Should().Be(4);
        // Tuned per row as skills get bodies, so only the shape is checked here - a row with no
        // cooldown at all would be castable every tick.
        skill.CooldownMs.Should().BePositive();
      }
    }
  }

  [Fact]
  public void SkillAssetIds_AreNotSharedBetweenHeroes() {
    var harness = SimHarness.CreateInitialized();
    int[] heroAssetIds = [
      AssetIds.HeroHairyWizard, AssetIds.HeroShroom, AssetIds.HeroCrystalGiant,
      AssetIds.HeroSkinwalker, AssetIds.HeroPickleKnight
    ];

    var seen = new HashSet<int>();
    foreach (var heroAssetId in heroAssetIds) {
      var heroAsset = harness.AssetRegistry.Get<HeroAsset>(heroAssetId);
      for (var slot = 0; slot < SkillsComponent.MaxSlots; slot++)
        seen.Add(heroAsset.GetSkillAssetId(slot))
          .Should().BeTrue("each hero owns its own rows so retuning one never touches another");
    }

    seen.Count.Should().Be(heroAssetIds.Length * SkillsComponent.MaxSlots);
  }

  internal static SkillAsset SkillInSlot(SimHarness harness, EntityRef hero, SkillSlot slot) {
    var frame = harness.Frame;
    var skillAssetId = frame.GetReadOnly<SkillsComponent>(hero).GetSkillAssetId((int)slot);
    return harness.AssetRegistry.Get<SkillAsset>(skillAssetId);
  }

  internal static void GrantPoints(ref Frame frame, EntityRef hero, int points) {
    frame.Get<SkillsComponent>(hero).SkillPoints = points;
  }
}
