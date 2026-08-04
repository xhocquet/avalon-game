using FluentAssertions;
using Meesles.Avalon.Sim.Components;
using Meesles.Avalon.Sim.Factories;
using Xunit;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Tests;

// StatsComponent.AttackSpeed scales the base period in Combat.AttackCooldownTicks. DamageSystem does the
// division at the moment of the hit, so the base period stays intact and buffs stack on the rate.
public class AttackSpeedTests {
  private const int BaseCooldownTicks = 30;

  [Theory]
  [InlineData(1.0f, BaseCooldownTicks)]
  [InlineData(2.0f, 15)]
  [InlineData(1.5f, 20)]
  [InlineData(0.5f, 60)]
  [InlineData(1.15f, 26)] // Rounds to nearest rather than truncating (26.09)
  public void AttackSpeed_ScalesTheCooldownAppliedAfterAHit(float attackSpeed, int expectedCooldown) {
    var harness = SimHarness.CreateInitialized();
    var attacker = SpawnDuel(harness, FP64.FromFloat(attackSpeed));

    TickUntilAttackLands(harness, attacker);

    harness.Frame.GetReadOnly<Combat>(attacker).CooldownRemainingTicks.Should().Be(expectedCooldown);
  }

  // A multiplier big enough to divide the period below a tick still leaves one tick between hits.
  [Fact]
  public void ExtremeAttackSpeed_StillLeavesOneTickBetweenHits() {
    var harness = SimHarness.CreateInitialized();
    var attacker = SpawnDuel(harness, FP64.FromInt(1000));

    TickUntilAttackLands(harness, attacker);

    harness.Frame.GetReadOnly<Combat>(attacker).CooldownRemainingTicks.Should().Be(1);
  }

  // Two hostile minions a unit apart, well inside the attacker's reach. Minion rather than bare
  // entities because TargetAcquisitionSystem only arms units it recognises.
  private static EntityRef SpawnDuel(SimHarness harness, FP64 attackSpeed) {
    var frame = harness.Frame;

    var attacker = SpawnMinion(ref frame, FPVector3.Zero, teamId: 1, health: 100);
    frame.Add(attacker, new StatsComponent { Strength = 10, AttackSpeed = attackSpeed });
    frame.Add(attacker, new Combat { AttackRange = FP64.FromInt(3), AttackCooldownTicks = BaseCooldownTicks });

    var target = SpawnMinion(ref frame, new FPVector3(FP64.One, FP64.Zero, FP64.Zero), teamId: 2, health: 10000);
    frame.Add(target, new StatsComponent { Strength = 0 });

    return attacker;
  }

  private static EntityRef SpawnMinion(ref Frame frame, FPVector3 position, int teamId, int health) {
    var entity = frame.CreateEntity();
    frame.Add(entity, TransformFactory.At(position));
    frame.Add(entity, new UnitIdComponent {
      UnitId = UnitLookup.NextUnitId(ref frame),
      UnitTypeId = SimulationSetup.MinionUnitTypeId
    });
    frame.Add(entity, new TeamComponent { TeamId = teamId });
    frame.Add(entity, new Minion { WaveId = 99 });
    frame.Add(entity, new Health(health));

    return entity;
  }

  // TargetAcquisitionSystem hands the attacker its target, so run on until the first hit lands.
  private static void TickUntilAttackLands(SimHarness harness, EntityRef attacker) {
    for (var tick = 0; tick < 10; tick++) {
      harness.Tick();
      if (harness.Frame.GetReadOnly<Combat>(attacker).CooldownRemainingTicks > 0)
        return;
    }

    throw new System.InvalidOperationException("Attacker never landed a hit.");
  }
}
