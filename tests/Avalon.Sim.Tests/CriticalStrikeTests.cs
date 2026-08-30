using FluentAssertions;
using Meesles.Avalon.Sim.Components;
using Xunit;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Tests;

// Crit is an auto-attack rule: DamageSystem opts in, skill damage does not. The roll is a pure
// function of (world seed, attacker unit id, tick), which is what keeps it rollback-safe.
public class CriticalStrikeTests {
  [Fact]
  public void ACrit_MultipliesDamageBeforeMitigation() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var attacker = harness.FindHero(1);
    var target = harness.FindHero(2);
    SetCrit(ref frame, attacker, chance: FP64.One, damage: FP64.FromInt(2));
    SetArmor(ref frame, target, 100); // Halves whatever arrives

    var dealt = DamageApplication.ApplyDamage(ref frame, attacker, target, FP64.FromInt(100),
      DamageType.Physical, canCrit: true);

    dealt.Should().Be(FP64.FromInt(100)); // 100 * 2 crit, then * 0.5 mitigation
  }

  [Fact]
  public void WithoutTheOptIn_DamageNeverCrits() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var attacker = harness.FindHero(1);
    var target = harness.FindHero(2);
    SetCrit(ref frame, attacker, chance: FP64.One, damage: FP64.FromInt(2));
    SetArmor(ref frame, target, 0);

    DamageApplication.ApplyDamage(ref frame, attacker, target, FP64.FromInt(100))
      .Should().Be(FP64.FromInt(100));
  }

  [Fact]
  public void ZeroCritChance_LeavesTheHitAlone() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var attacker = harness.FindHero(1);
    var target = harness.FindHero(2);
    SetCrit(ref frame, attacker, chance: FP64.Zero, damage: FP64.FromInt(2));
    SetArmor(ref frame, target, 0);

    DamageApplication.ApplyDamage(ref frame, attacker, target, FP64.FromInt(100),
      DamageType.Physical, canCrit: true).Should().Be(FP64.FromInt(100));
  }

  // Same seed, same attacker, same tick: a resimulated tick must reach the same verdict.
  [Fact]
  public void TheRoll_IsStableForOneAttackerAndTick() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var attacker = harness.FindHero(1);
    SetCrit(ref frame, attacker, chance: FP64.One / FP64.FromInt(2), damage: FP64.FromInt(2));
    var unitId = UnitLookup.GetUnitId(ref frame, attacker);

    var first = CriticalStrikes.Rolls(ref frame, attacker, unitId);
    for (var i = 0; i < 8; i++)
      CriticalStrikes.Rolls(ref frame, attacker, unitId).Should().Be(first);
  }

  // A fixed roll per tick would make a coin-flip crit either always or never fire; the tick has to
  // reach the stream.
  [Fact]
  public void AHalfChance_LandsSomewhereNearHalfAcrossTicks() {
    var harness = SimHarness.CreateInitialized();
    var startFrame = harness.Frame;
    var attacker = harness.FindHero(1);
    var unitId = UnitLookup.GetUnitId(ref startFrame, attacker);
    var half = FP64.One / FP64.FromInt(2);

    var crits = 0;
    for (var tick = 0; tick < 200; tick++) {
      var frame = harness.Frame;
      SetCrit(ref frame, attacker, chance: half, damage: FP64.FromInt(2));
      if (CriticalStrikes.Rolls(ref frame, attacker, unitId))
        crits++;

      harness.Tick();
    }

    crits.Should().BeInRange(70, 130);
  }

  private static void SetCrit(ref Frame frame, EntityRef entity, FP64 chance, FP64 damage) {
    ref var stats = ref frame.Get<Stats>(entity);
    stats.Set(StatType.CritChance, chance);
    stats.Set(StatType.CritDamage, damage);
  }

  private static void SetArmor(ref Frame frame, EntityRef entity, int armor) {
    frame.Get<Stats>(entity).Set(StatType.Armor, FP64.FromInt(armor));
  }
}
