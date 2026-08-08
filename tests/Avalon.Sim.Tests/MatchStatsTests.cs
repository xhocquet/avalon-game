using FluentAssertions;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using Meesles.Avalon.Sim.Factories;
using Xunit;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Tests;

public class MatchStatsTests {
  [Fact]
  public void RecordDamage_CreditsPostMitigationDamageAgainstHostilesOnly() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var attacker = harness.FindHero(playerId: 1);
    var victim = harness.FindHero(playerId: 2);

    var dealt = DamageApplication.ApplyDamage(ref frame, attacker, victim, 50);

    dealt.Should().BeGreaterThan(0);
    frame.GetReadOnly<Player>(attacker).DamageDealt.Should().Be(dealt);
  }

  [Fact]
  public void RecordDamage_IgnoresFriendlyFire() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var attacker = harness.FindHero(playerId: 1);

    DamageApplication.ApplyDamage(ref frame, attacker, attacker, 50);

    frame.GetReadOnly<Player>(attacker).DamageDealt.Should().Be(0);
  }

  [Fact]
  public void RecordKill_CountsByVictimTypeAndPaysTheAssetsScore() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var rules = frame.AssetRegistry.Get<MatchRulesAsset>();
    var killer = harness.FindHero(playerId: 1);
    const int enemyTeamId = 2;

    MatchStats.RecordKill(ref frame, killer, SimulationSetup.PlayerUnitTypeId, enemyTeamId);
    MatchStats.RecordKill(ref frame, killer, SimulationSetup.MinionUnitTypeId, enemyTeamId);
    MatchStats.RecordKill(ref frame, killer, SimulationSetup.TurretUnitTypeId, enemyTeamId);
    MatchStats.RecordKill(ref frame, killer, SimulationSetup.CrystalUnitTypeId, enemyTeamId);

    ref readonly var record = ref frame.GetReadOnly<Player>(killer);
    record.HeroKills.Should().Be(1);
    record.MinionKills.Should().Be(1);
    record.StructureKills.Should().Be(2); // turret and crystal both count as structures
    record.Score.Should().Be(
      rules.HeroKillScore + rules.MinionKillScore + rules.StructureKillScore * 2);
  }

  [Fact]
  public void RecordKill_PaysNothingForKillingYourOwn() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var killer = harness.FindHero(playerId: 1);
    var ownTeamId = frame.GetReadOnly<TeamComponent>(killer).TeamId;

    MatchStats.RecordKill(ref frame, killer, SimulationSetup.MinionUnitTypeId, ownTeamId);

    frame.GetReadOnly<Player>(killer).MinionKills.Should().Be(0);
    frame.GetReadOnly<Player>(killer).Score.Should().Be(0);
  }

  [Fact]
  public void BeginRespawn_CreditsTheKillerAndChargesTheVictimTheDeath() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var killer = harness.FindHero(playerId: 1);
    var victim = harness.FindHero(playerId: 2);
    var rules = frame.AssetRegistry.Get<MatchRulesAsset>();

    // Route the kill through the damage path so the credit rides LastDamagerUnitId the way it does live.
    frame.Get<Health>(victim).Current = 1;
    DamageApplication.ApplyDamage(ref frame, killer, victim, 9999);
    new RespawnSystem().Update(ref frame);

    frame.GetReadOnly<Player>(killer).HeroKills.Should().Be(1);
    frame.GetReadOnly<Player>(killer).Score.Should().Be(rules.HeroKillScore);
    frame.GetReadOnly<Player>(victim).Deaths.Should().Be(1);
    frame.GetReadOnly<Player>(victim).Score.Should().Be(-rules.DeathScorePenalty);
  }

  [Fact]
  public void DeathSystem_CreditsAMinionKillToTheHeroThatLandedTheFatalHit() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var killer = harness.FindHero(playerId: 1);
    var minion = SpawnEnemyMinion(ref frame, harness);

    DamageApplication.ApplyDamage(ref frame, killer, minion, 9999);
    new DeathSystem().Update(ref frame);

    frame.GetReadOnly<Player>(killer).MinionKills.Should().Be(1);
  }

  private static EntityRef SpawnEnemyMinion(ref Frame frame, SimHarness harness) {
    var killerTeamId = frame.GetReadOnly<TeamComponent>(harness.FindHero(playerId: 1)).TeamId;

    var entity = frame.CreateEntity();
    frame.Add(entity, TransformFactory.At(FPVector3.Zero));
    frame.Add(entity, new UnitIdComponent {
      UnitId = UnitLookup.NextUnitId(ref frame),
      UnitTypeId = SimulationSetup.MinionUnitTypeId
    });
    frame.Add(entity, new TeamComponent { TeamId = killerTeamId + 1 });
    frame.Add(entity, new Minion { WaveId = 99 });
    frame.Add(entity, new Health(10));

    return entity;
  }
}
