using FluentAssertions;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using Meesles.Avalon.Sim.Factories;
using Xunit;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Tests;

// Both ways gold arrives: the passive trickle (off for the opening minute) and kill bounties. The
// trickle tests run on a 1s tick so the delay lands a few dozen ticks in instead of a few thousand.
public class GoldAccrualTests {
  private const int OneSecondTickMs = 1000;

  [Fact]
  public void Gold_DoesNotAccrue_BeforeTheStartDelay() {
    var harness = SimHarness.CreateInitialized(deltaTimeMs: OneSecondTickMs);
    var rules = harness.AssetRegistry.Get<MatchRulesAsset>();
    var delaySeconds = rules.GoldStartDelayMs / OneSecondTickMs;

    delaySeconds.Should().BeGreaterThan(1, "the delay must be long enough for this test to mean anything");

    for (var i = 0; i < delaySeconds - 1; i++)
      harness.Tick();

    GoldOf(harness).Should().Be(rules.StartingGold);
  }

  [Fact]
  public void Gold_Accrues_AfterTheStartDelay() {
    var harness = SimHarness.CreateInitialized(deltaTimeMs: OneSecondTickMs);
    var rules = harness.AssetRegistry.Get<MatchRulesAsset>();
    var elapsedSeconds = rules.GoldStartDelayMs / OneSecondTickMs + 10;

    for (var i = 0; i < elapsedSeconds; i++)
      harness.Tick();

    GoldOf(harness).Should().BeGreaterThan(rules.StartingGold);
  }

  [Fact]
  public void MinionKill_PaysTheBountyToTheHeroThatLandedTheBlow() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var gold = harness.AssetRegistry.Get<GoldRulesAsset>();
    var killer = harness.FindHero(playerId: 1);
    var enemy = harness.FindHero(playerId: 2);
    var startingGold = frame.GetReadOnly<Inventory>(killer).Gold;

    KillMinion(ref frame, frame.GetReadOnly<Team>(enemy).TeamId, killer);

    frame.GetReadOnly<Inventory>(killer).Gold.Should().Be(startingGold + gold.GoldPerMinionKill);
    frame.GetReadOnly<Inventory>(enemy).Gold.Should().Be(startingGold);
  }

  [Fact]
  public void HeroKill_PaysTheHeroBountyThroughRespawnSystem() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var gold = harness.AssetRegistry.Get<GoldRulesAsset>();
    var killer = harness.FindHero(playerId: 1);
    var victim = harness.FindHero(playerId: 2);
    var startingGold = frame.GetReadOnly<Inventory>(killer).Gold;

    ref var victimHealth = ref frame.Get<Health>(victim);
    victimHealth.Current = FP64.Zero;
    victimHealth.LastDamagerUnitId = frame.GetReadOnly<UnitIdentity>(killer).UnitId;

    new RespawnSystem().Update(ref frame);

    frame.GetReadOnly<Inventory>(killer).Gold.Should().Be(startingGold + gold.GoldPerHeroKill);
    frame.GetReadOnly<Inventory>(victim).Gold.Should().Be(startingGold);
  }

  [Fact]
  public void TurretAndCrystalKills_PayTheirOwnBounties() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var gold = harness.AssetRegistry.Get<GoldRulesAsset>();
    var hero = harness.FindHero(playerId: 1);
    var victimTeamId = frame.GetReadOnly<Team>(harness.FindHero(playerId: 2)).TeamId;
    var startingGold = frame.GetReadOnly<Inventory>(hero).Gold;

    GoldRewards.AwardForKill(ref frame, hero, SimulationSetup.TurretUnitTypeId, victimTeamId);
    frame.GetReadOnly<Inventory>(hero).Gold.Should().Be(startingGold + gold.GoldPerTurretKill);

    GoldRewards.AwardForKill(ref frame, hero, SimulationSetup.CrystalUnitTypeId, victimTeamId);
    frame.GetReadOnly<Inventory>(hero).Gold
      .Should().Be(startingGold + gold.GoldPerTurretKill + gold.GoldPerCrystalKill);
  }

  [Fact]
  public void FriendlyFireKill_PaysNothing() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var hero = harness.FindHero(playerId: 1);
    var startingGold = frame.GetReadOnly<Inventory>(hero).Gold;

    KillMinion(ref frame, frame.GetReadOnly<Team>(hero).TeamId, hero);

    frame.GetReadOnly<Inventory>(hero).Gold.Should().Be(startingGold);
  }

  // A minion lands the fatal hit. Minions carry no wallet, so the bounty is dropped rather than
  // routed to the hero on their team - same rule XP pays on.
  [Fact]
  public void MinionLastHit_PaysNothingToTheHeroOnItsTeam() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var hero = harness.FindHero(playerId: 1);
    var victimTeamId = frame.GetReadOnly<Team>(harness.FindHero(playerId: 2)).TeamId;
    var startingGold = frame.GetReadOnly<Inventory>(hero).Gold;

    var killerMinion = SpawnMinion(ref frame, frame.GetReadOnly<Team>(hero).TeamId);
    KillMinion(ref frame, victimTeamId, killerMinion);

    frame.GetReadOnly<Inventory>(hero).Gold.Should().Be(startingGold);
  }

  [Fact]
  public void UnattributedKill_PaysNothing() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var hero = harness.FindHero(playerId: 1);
    var startingGold = frame.GetReadOnly<Inventory>(hero).Gold;

    var victim = SpawnMinion(ref frame, frame.GetReadOnly<Team>(harness.FindHero(2)).TeamId);
    frame.Get<Health>(victim).Current = FP64.Zero;
    new DeathSystem().Update(ref frame);

    frame.GetReadOnly<Inventory>(hero).Gold.Should().Be(startingGold);
  }

  private static void KillMinion(ref Frame frame, int victimTeamId, EntityRef killer) {
    var victim = SpawnMinion(ref frame, victimTeamId);

    ref var health = ref frame.Get<Health>(victim);
    health.Current = FP64.Zero;
    health.LastDamagerUnitId = frame.GetReadOnly<UnitIdentity>(killer).UnitId;

    new DeathSystem().Update(ref frame);
  }

  private static EntityRef SpawnMinion(ref Frame frame, int teamId) {
    var entity = frame.CreateEntity();

    frame.Add(entity, TransformFactory.At(FPVector3.Zero));
    frame.Add(entity, new UnitIdentity {
      UnitId = UnitLookup.NextUnitId(ref frame),
      UnitTypeId = SimulationSetup.MinionUnitTypeId,
    });
    frame.Add(entity, new Team { TeamId = teamId });
    frame.Add(entity, new Minion { WaveId = 99 });
    frame.Add(entity, new Health(100));

    return entity;
  }

  private static int GoldOf(SimHarness harness) {
    var frame = harness.Frame;
    return frame.GetReadOnly<Inventory>(harness.FindHero(playerId: 1)).Gold;
  }
}
