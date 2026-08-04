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
    int baseStrength = frame.GetReadOnly<StatsComponent>(hero).Strength;
    int baseMaxHealth = frame.GetReadOnly<StatsComponent>(hero).MaxHealth;
    FP64 baseAttackSpeed = frame.GetReadOnly<StatsComponent>(hero).AttackSpeed;
    int currentHealth = frame.GetReadOnly<Health>(hero).Current;
    frame.Get<ExperienceComponent>(hero).Experience = rules.TotalXpForLevel(2);

    new ExperienceSystem().Update(ref frame);

    ref readonly var stats = ref frame.GetReadOnly<StatsComponent>(hero);
    stats.Strength.Should().Be(baseStrength + rules.StrengthPerLevel);
    stats.MaxHealth.Should().Be(baseMaxHealth + rules.MaxHealthPerLevel);
    stats.AttackSpeed.Should().Be(baseAttackSpeed + rules.AttackSpeedPerLevel);
    frame.GetReadOnly<Health>(hero).Current.Should().Be(currentHealth + rules.MaxHealthPerLevel);
  }

  [Fact]
  public void EnoughXpForSeveralLevels_AppliesEveryLevelInOneTick() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var rules = harness.AssetRegistry.Get<XpRulesAsset>();
    EntityRef hero = harness.FindHero(1);
    int baseStrength = frame.GetReadOnly<StatsComponent>(hero).Strength;
    frame.Get<ExperienceComponent>(hero).Experience = rules.TotalXpForLevel(4);

    var collector = new EventCollector();
    collector.BeginTick(3);
    frame.EventRaiser = collector;

    new ExperienceSystem().Update(ref frame);

    frame.GetReadOnly<ExperienceComponent>(hero).Level.Should().Be(4);
    frame.GetReadOnly<StatsComponent>(hero).Strength.Should().Be(baseStrength + rules.StrengthPerLevel * 3);
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
    frame.Get<Health>(hero).Current = 0;
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
    frame.Get<Health>(hero).Current = 0;

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
}
