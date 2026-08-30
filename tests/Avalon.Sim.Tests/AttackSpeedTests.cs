using FluentAssertions;
using Meesles.Avalon.Sim.Components;
using Meesles.Avalon.Sim.Factories;
using Xunit;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Tests;

// Attack rate is authored in attacks per second (BaseAttackSpeed, scaled by BonusAttackSpeed) and
// DamageSystem turns it into a tick cooldown at the moment of the hit, so buffs stay additive on the
// rate and the authored number keeps its meaning if the tick rate changes.
public class AttackSpeedTests {
  // The harness runs at SimHarness.DefaultDeltaTimeMs (16ms), so a second is 62.5 ticks.
  private const int TicksPerSecondNumerator = 1000;

  [Theory]
  [InlineData(1.0f, 0.0f, 63)] // 62.5 rounds up
  [InlineData(2.0f, 0.0f, 31)] // 31.25 rounds down
  [InlineData(0.5f, 0.0f, 125)]
  [InlineData(0.67f, 0.0f, 93)] // Crystal Giant's base rate
  [InlineData(1.0f, 0.5f, 42)] // +50% bonus: 1.5 attacks/sec is 41.67 ticks
  [InlineData(1.0f, -0.5f, 125)] // A slow halves the rate the same way
  public void AttackSpeed_SetsTheCooldownAppliedAfterAHit(float baseAttackSpeed, float bonus,
    int expectedCooldown) {
    var harness = SimHarness.CreateInitialized();
    var attacker = SpawnDuel(harness, FP64.FromFloat(baseAttackSpeed), FP64.FromFloat(bonus));

    TickUntilAttackLands(harness, attacker);

    harness.Frame.GetReadOnly<Combat>(attacker).CooldownRemainingTicks.Should().Be(expectedCooldown);
  }

  // Stacking bonus attack speed past the cap stops mattering rather than driving the cooldown to
  // zero, which is what the StatRanges ceiling on the derived rate is for.
  [Fact]
  public void ExtremeAttackSpeed_IsHeldAtTheRateCap() {
    var harness = SimHarness.CreateInitialized();
    var attacker = SpawnDuel(harness, FP64.FromInt(4), FP64.FromInt(4));

    TickUntilAttackLands(harness, attacker);

    // The cap is 2.5 attacks/sec, so 62.5 / 2.5 = 25 ticks.
    harness.Frame.GetReadOnly<Combat>(attacker).CooldownRemainingTicks.Should().Be(25);
  }

  [Fact]
  public void TicksPerSecondAssumption_MatchesTheHarness() {
    (TicksPerSecondNumerator / SimHarness.DefaultDeltaTimeMs).Should().Be(62); // 62.5 truncated
  }

  // Two hostile minions a unit apart, well inside the attacker's reach. Minion rather than bare
  // entities because TargetAcquisitionSystem only arms units it recognises.
  private static EntityRef SpawnDuel(SimHarness harness, FP64 baseAttackSpeed, FP64 bonus) {
    var frame = harness.Frame;

    var attacker = SpawnMinion(ref frame, FPVector3.Zero, teamId: 1, health: 100);
    frame.Add(attacker, Stats.Create()
      .With(StatType.AttackDamage, FP64.FromInt(10))
      .With(StatType.BaseAttackSpeed, baseAttackSpeed)
      .With(StatType.BonusAttackSpeed, bonus)
      .With(StatType.AttackRange, FP64.FromInt(3))
      .With(StatType.AcquisitionRange, FP64.FromInt(9)));
    frame.Add(attacker, new Combat());

    var target = SpawnMinion(ref frame, new FPVector3(FP64.One, FP64.Zero, FP64.Zero), teamId: 2,
      health: 10000);
    frame.Add(target, Stats.Create()
      .With(StatType.MaxHealth, FP64.FromInt(10000))
      .With(StatType.AttackDamage, FP64.Zero));

    return attacker;
  }

  private static EntityRef SpawnMinion(ref Frame frame, FPVector3 position, int teamId, int health) {
    var entity = frame.CreateEntity();
    frame.Add(entity, TransformFactory.At(position));
    frame.Add(entity, new UnitIdentity {
      UnitId = UnitLookup.NextUnitId(ref frame),
      UnitTypeId = SimulationSetup.MinionUnitTypeId
    });
    frame.Add(entity, new Team { TeamId = teamId });
    frame.Add(entity, new Minion { WaveId = 99 });
    frame.Add(entity, new Health(FP64.FromInt(health)));

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
