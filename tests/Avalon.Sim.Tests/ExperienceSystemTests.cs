using FluentAssertions;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using Meesles.Avalon.Sim.Factories;
using Xunit;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Tests;

public class ExperienceSystemTests {
  [Fact]
  public void HeroesStartAtLevelOneWithNoExperience() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    EntityRef hero = harness.FindHero(1);

    ref readonly var experience = ref frame.GetReadOnly<ExperienceComponent>(hero);
    experience.Level.Should().Be(1);
    experience.Experience.Should().Be(0);
  }

  [Fact]
  public void MinionKill_AwardsExperienceToTheHeroThatLandedTheBlow() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var rules = harness.AssetRegistry.Get<XpRulesAsset>();
    EntityRef killerHero = harness.FindHero(1);
    EntityRef enemyHero = harness.FindHero(2);
    int victimTeamId = frame.GetReadOnly<TeamComponent>(enemyHero).TeamId;

    KillMinion(ref frame, victimTeamId, killerHero);

    frame.GetReadOnly<ExperienceComponent>(killerHero).Experience.Should().Be(rules.XpPerMinionKill);
    frame.GetReadOnly<ExperienceComponent>(enemyHero).Experience.Should().Be(0);
  }

  [Fact]
  public void MinionLastHit_AwardsNothingToTheHeroOnItsTeam() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    EntityRef hero = harness.FindHero(1);
    int killerTeamId = frame.GetReadOnly<TeamComponent>(hero).TeamId;
    int victimTeamId = frame.GetReadOnly<TeamComponent>(harness.FindHero(2)).TeamId;

    // The fatal hit comes from a minion. Minions carry no ExperienceComponent, so the XP is dropped
    // rather than routed to the hero that owns them.
    EntityRef killerMinion = SpawnMinion(ref frame, killerTeamId);
    KillMinion(ref frame, victimTeamId, killerMinion);

    frame.GetReadOnly<ExperienceComponent>(hero).Experience.Should().Be(0);
  }

  [Fact]
  public void FriendlyFireKill_AwardsNothing() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    EntityRef hero = harness.FindHero(1);
    int teamId = frame.GetReadOnly<TeamComponent>(hero).TeamId;

    KillMinion(ref frame, teamId, hero);

    frame.GetReadOnly<ExperienceComponent>(hero).Experience.Should().Be(0);
  }

  [Fact]
  public void UnattributedKill_AwardsNothing() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    EntityRef hero = harness.FindHero(1);
    int victimTeamId = frame.GetReadOnly<TeamComponent>(harness.FindHero(2)).TeamId;

    EntityRef victim = SpawnMinion(ref frame, victimTeamId);
    frame.Get<Health>(victim).Current = 0;
    new DeathSystem().Update(ref frame);

    frame.GetReadOnly<ExperienceComponent>(hero).Experience.Should().Be(0);
  }

  [Fact]
  public void KillerDyingOnTheSameTick_StillEarnsTheKill() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var rules = harness.AssetRegistry.Get<XpRulesAsset>();
    EntityRef hero = harness.FindHero(1);
    int victimTeamId = frame.GetReadOnly<TeamComponent>(harness.FindHero(2)).TeamId;

    EntityRef victim = SpawnMinion(ref frame, victimTeamId);
    ref var victimHealth = ref frame.Get<Health>(victim);
    victimHealth.Current = 0;
    victimHealth.LastDamagerUnitId = frame.GetReadOnly<UnitIdComponent>(hero).UnitId;

    // A second corpse ahead of the victim in the pass: the award must not depend on destroy order.
    EntityRef bystander = SpawnMinion(ref frame, victimTeamId);
    frame.Get<Health>(bystander).Current = 0;

    new DeathSystem().Update(ref frame);

    frame.GetReadOnly<ExperienceComponent>(hero).Experience.Should().Be(rules.XpPerMinionKill);
  }

  [Fact]
  public void UnitDiedEvent_CarriesTheKillersUnitIdAndType() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    EntityRef hero = harness.FindHero(1);
    int victimTeamId = frame.GetReadOnly<TeamComponent>(harness.FindHero(2)).TeamId;
    EntityRef victim = SpawnMinion(ref frame, victimTeamId);
    ref var victimHealth = ref frame.Get<Health>(victim);
    victimHealth.Current = 0;
    victimHealth.LastDamagerUnitId = frame.GetReadOnly<UnitIdComponent>(hero).UnitId;

    var collector = new EventCollector();
    collector.BeginTick(5);
    frame.EventRaiser = collector;

    new DeathSystem().Update(ref frame);

    var evt = collector.Collected[0].Should().BeOfType<UnitDiedEvent>().Subject;
    evt.DestroyerUnitId.Should().Be(frame.GetReadOnly<UnitIdComponent>(hero).UnitId);
    evt.DestroyerUnitTypeId.Should().Be(SimulationSetup.PlayerUnitTypeId);
  }

  [Fact]
  public void UnitDiedEvent_LeavesTheKillerZeroWhenNothingDealtDamage() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    EntityRef victim = SpawnMinion(ref frame, 2);
    frame.Get<Health>(victim).Current = 0;

    var collector = new EventCollector();
    collector.BeginTick(5);
    frame.EventRaiser = collector;

    new DeathSystem().Update(ref frame);

    var evt = collector.Collected[0].Should().BeOfType<UnitDiedEvent>().Subject;
    evt.DestroyerUnitId.Should().Be(0);
    evt.DestroyerUnitTypeId.Should().Be(0);
  }

  [Fact]
  public void ReachingTheThreshold_LevelsUpAndRaisesEvent() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var rules = harness.AssetRegistry.Get<XpRulesAsset>();
    EntityRef hero = harness.FindHero(1);
    int unitId = frame.GetReadOnly<UnitIdComponent>(hero).UnitId;
    frame.Get<ExperienceComponent>(hero).Experience = rules.TotalXpForLevel(2);

    var collector = new EventCollector();
    collector.BeginTick(11);
    frame.EventRaiser = collector;

    new ExperienceSystem().Update(ref frame);

    frame.GetReadOnly<ExperienceComponent>(hero).Level.Should().Be(2);
    var evt = collector.Collected[0].Should().BeOfType<HeroLeveledUpEvent>().Subject;
    evt.Tick.Should().Be(11);
    evt.UnitId.Should().Be(unitId);
    evt.PlayerId.Should().Be(1);
    evt.Level.Should().Be(2);
  }

  [Fact]
  public void OneXpShortOfTheThreshold_DoesNotLevelUp() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var rules = harness.AssetRegistry.Get<XpRulesAsset>();
    EntityRef hero = harness.FindHero(1);
    frame.Get<ExperienceComponent>(hero).Experience = rules.TotalXpForLevel(2) - 1;

    new ExperienceSystem().Update(ref frame);

    frame.GetReadOnly<ExperienceComponent>(hero).Level.Should().Be(1);
  }

  [Fact]
  public void LevelUp_AppliesStatGainsAndHealsTheMaxHealthDelta() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var rules = harness.AssetRegistry.Get<XpRulesAsset>();
    EntityRef hero = harness.FindHero(1);
    var heroAsset = HeroRow(harness, hero);
    var baseAttackDamage = frame.GetReadOnly<StatsComponent>(hero).AttackDamage;
    var baseMaxHealth = frame.GetReadOnly<StatsComponent>(hero).MaxHealth;
    var baseArmor = frame.GetReadOnly<StatsComponent>(hero).Armor;
    var currentHealth = frame.GetReadOnly<Health>(hero).Current;
    frame.Get<ExperienceComponent>(hero).Experience = rules.TotalXpForLevel(2);

    new ExperienceSystem().Update(ref frame);

    var healthGain = StatGrowth.Between(rules, heroAsset.HealthPerLevel, 1, 2);
    ref readonly var stats = ref frame.GetReadOnly<StatsComponent>(hero);
    stats.AttackDamage.Should()
      .Be(baseAttackDamage + StatGrowth.Between(rules, heroAsset.AttackDamagePerLevel, 1, 2));
    stats.MaxHealth.Should().Be(baseMaxHealth + healthGain);
    stats.Armor.Should().Be(baseArmor + StatGrowth.Between(rules, heroAsset.ArmorPerLevel, 1, 2));
    frame.GetReadOnly<Health>(hero).Current.Should().Be(currentHealth + healthGain);
  }

  // The curve is not linear, so the gain has to be the difference between the two levels rather
  // than a per-level step repeated - otherwise a multi-level tick lands somewhere else entirely.
  [Fact]
  public void LevellingSeveralStepsAtOnce_LandsWhereLevellingOneAtATimeDoes() {
    var stepwise = SimHarness.CreateInitialized();
    var atOnce = SimHarness.CreateInitialized();
    var rules = stepwise.AssetRegistry.Get<XpRulesAsset>();

    var stepwiseFrame = stepwise.Frame;
    var stepwiseHero = stepwise.FindHero(1);
    for (var level = 2; level <= 6; level++) {
      stepwiseFrame.Get<ExperienceComponent>(stepwiseHero).Experience = rules.TotalXpForLevel(level);
      new ExperienceSystem().Update(ref stepwiseFrame);
    }

    var atOnceFrame = atOnce.Frame;
    var atOnceHero = atOnce.FindHero(1);
    atOnceFrame.Get<ExperienceComponent>(atOnceHero).Experience = rules.TotalXpForLevel(6);
    new ExperienceSystem().Update(ref atOnceFrame);

    // Within a raw fixed-point unit or two: five separate Adds each round once, one Add rounds once.
    // The residue is 2^-32 scale and identical on both peers, so it cannot desync a rollback.
    for (var stat = 0; stat < StatRanges.Count; stat++) {
      var atOnceValue = atOnceFrame.GetReadOnly<StatsComponent>(atOnceHero).Get((StatType)stat);
      var stepwiseValue = stepwiseFrame.GetReadOnly<StatsComponent>(stepwiseHero).Get((StatType)stat);
      FP64.Abs(atOnceValue - stepwiseValue).Should().BeLessThanOrEqualTo(FP64.FromRaw(8),
        $"{(StatType)stat} must not depend on how the levels arrived");
    }
  }

  // The authored ranges are level 1 to MaxLevel spans, so a capped hero has to land exactly on
  // base + perLevel * (MaxLevel - 1). That is what makes StatGrowthCurveA/B readable as authored
  // data rather than an arbitrary pair.
  [Fact]
  public void AtTheLevelCap_AStatLandsOnBasePlusItsWholeGrowth() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var rules = harness.AssetRegistry.Get<XpRulesAsset>();
    EntityRef hero = harness.FindHero(1);
    var heroAsset = HeroRow(harness, hero);
    frame.Get<ExperienceComponent>(hero).Experience = rules.TotalXpForLevel(rules.MaxLevel);

    new ExperienceSystem().Update(ref frame);

    // Within a raw unit: the curve constants are authored as decimals, so the ramp reaches 1 at the
    // cap only to fixed-point precision.
    var levels = FP64.FromInt(rules.MaxLevel - 1);
    ShouldBeAbout(frame.GetReadOnly<StatsComponent>(hero).MaxHealth,
      heroAsset.BaseHealth + heroAsset.HealthPerLevel * levels);
    ShouldBeAbout(frame.GetReadOnly<StatsComponent>(hero).Armor,
      heroAsset.BaseArmor + heroAsset.ArmorPerLevel * levels);
  }

  [Fact]
  public void EnoughXpForSeveralLevels_AppliesEveryLevelInOneTick() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var rules = harness.AssetRegistry.Get<XpRulesAsset>();
    EntityRef hero = harness.FindHero(1);
    var heroAsset = HeroRow(harness, hero);
    var baseAttackDamage = frame.GetReadOnly<StatsComponent>(hero).AttackDamage;
    frame.Get<ExperienceComponent>(hero).Experience = rules.TotalXpForLevel(4);

    var collector = new EventCollector();
    collector.BeginTick(3);
    frame.EventRaiser = collector;

    new ExperienceSystem().Update(ref frame);

    frame.GetReadOnly<ExperienceComponent>(hero).Level.Should().Be(4);
    frame.GetReadOnly<StatsComponent>(hero).AttackDamage.Should()
      .Be(baseAttackDamage + StatGrowth.Between(rules, heroAsset.AttackDamagePerLevel, 1, 4));
    // Three levels in one tick is still one arrival, so the view gets one event carrying the level reached.
    collector.Count.Should().Be(1);
    collector.Collected[0].Should().BeOfType<HeroLeveledUpEvent>().Subject.Level.Should().Be(4);
  }

  [Fact]
  public void Level_IsCappedAtMaxLevel() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var rules = harness.AssetRegistry.Get<XpRulesAsset>();
    EntityRef hero = harness.FindHero(1);
    frame.Get<ExperienceComponent>(hero).Experience = rules.TotalXpForLevel(rules.MaxLevel) * 10;

    new ExperienceSystem().Update(ref frame);

    frame.GetReadOnly<ExperienceComponent>(hero).Level.Should().Be(rules.MaxLevel);
  }

  [Fact]
  public void LevelUp_DoesNotReviveAHeroWaitingOnRespawn() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var rules = harness.AssetRegistry.Get<XpRulesAsset>();
    EntityRef hero = harness.FindHero(1);
    frame.Get<Health>(hero).Current = FP64.Zero;
    frame.Add(hero, new PendingRespawn { RemainingTicks = 30 });
    frame.Get<ExperienceComponent>(hero).Experience = rules.TotalXpForLevel(2);

    new ExperienceSystem().Update(ref frame);

    frame.GetReadOnly<ExperienceComponent>(hero).Level.Should().Be(2);
    frame.GetReadOnly<Health>(hero).Current.Should().Be(0);
  }

  [Fact]
  public void HeroKill_AwardsHeroExperienceThroughRespawnSystem() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var rules = harness.AssetRegistry.Get<XpRulesAsset>();
    EntityRef killer = harness.FindHero(1);
    EntityRef victim = harness.FindHero(2);
    ref var victimHealth = ref frame.Get<Health>(victim);
    victimHealth.Current = 0;
    victimHealth.LastDamagerUnitId = frame.GetReadOnly<UnitIdComponent>(killer).UnitId;

    new RespawnSystem().Update(ref frame);

    frame.GetReadOnly<ExperienceComponent>(killer).Experience.Should().Be(rules.XpPerHeroKill);
    frame.GetReadOnly<ExperienceComponent>(victim).Experience.Should().Be(0);
  }

  [Fact]
  public void Experience_SurvivesDeathAndRespawn() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    EntityRef hero = harness.FindHero(1);
    frame.Get<ExperienceComponent>(hero).Experience = 250;
    frame.Get<Health>(hero).Current = FP64.Zero;

    var system = new RespawnSystem();
    system.Update(ref frame);
    for (int i = 0; i < 600 && frame.Has<PendingRespawn>(hero); i++)
      system.Update(ref frame);

    frame.Has<PendingRespawn>(hero).Should().BeFalse();
    frame.GetReadOnly<ExperienceComponent>(hero).Experience.Should().Be(250);
  }

  [Fact]
  public void TurretAndCrystalKills_AwardTheirOwnRates() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var rules = harness.AssetRegistry.Get<XpRulesAsset>();
    EntityRef hero = harness.FindHero(1);
    int victimTeamId = frame.GetReadOnly<TeamComponent>(harness.FindHero(2)).TeamId;

    ExperienceRewards.AwardForKill(ref frame, hero, SimulationSetup.TurretUnitTypeId, victimTeamId);
    frame.GetReadOnly<ExperienceComponent>(hero).Experience.Should().Be(rules.XpPerTurretKill);

    ExperienceRewards.AwardForKill(ref frame, hero, SimulationSetup.CrystalUnitTypeId, victimTeamId);
    frame.GetReadOnly<ExperienceComponent>(hero).Experience
      .Should().Be(rules.XpPerTurretKill + rules.XpPerCrystalKill);
  }

  [Fact]
  public void TotalXpForLevel_MatchesTheAuthoredArithmeticCurve() {
    var harness = SimHarness.CreateInitialized();
    var rules = harness.AssetRegistry.Get<XpRulesAsset>();

    rules.TotalXpForLevel(1).Should().Be(0);
    rules.TotalXpForLevel(2).Should().Be(rules.XpToSecondLevel);
    rules.TotalXpForLevel(3).Should().Be(rules.XpToSecondLevel * 2 + rules.XpPerLevelIncrement);
    rules.TotalXpForLevel(4).Should().Be(rules.XpToSecondLevel * 3 + rules.XpPerLevelIncrement * 3);
  }

  // Spawns a victim minion, records `killer` as the fatal damager, and runs DeathSystem.
  private static void KillMinion(ref Frame frame, int victimTeamId, EntityRef killer) {
    EntityRef victim = SpawnMinion(ref frame, victimTeamId);

    ref var health = ref frame.Get<Health>(victim);
    health.Current = 0;
    health.LastDamagerUnitId = frame.GetReadOnly<UnitIdComponent>(killer).UnitId;

    new DeathSystem().Update(ref frame);
  }

  private static EntityRef SpawnMinion(ref Frame frame, int teamId) {
    var entity = frame.CreateEntity();

    frame.Add(entity, TransformFactory.At(FPVector3.Zero));
    frame.Add(entity, new UnitIdComponent {
      UnitId = UnitLookup.NextUnitId(ref frame),
      UnitTypeId = SimulationSetup.MinionUnitTypeId,
    });
    frame.Add(entity, new TeamComponent { TeamId = teamId });
    frame.Add(entity, new Minion { WaveId = 99 });
    frame.Add(entity, new Health(100));

    return entity;
  }

  private static void ShouldBeAbout(FP64 actual, FP64 expected) {
    // A thousandth of a point. The curve constants are authored as decimals, so the ramp reaches 1
    // at the cap only to fixed-point precision, and 17 levels of that compound.
    var tolerance = FP64.One / FP64.FromInt(1000);
    FP64.Abs(actual - expected).Should().BeLessThanOrEqualTo(tolerance,
      $"expected about {expected} but found {actual}");
  }

  private static HeroAsset HeroRow(SimHarness harness, EntityRef hero) {
    var frame = harness.Frame;
    return harness.AssetRegistry.Get<HeroAsset>(frame.GetReadOnly<Hero>(hero).HeroAssetId);
  }
}
