using FluentAssertions;
using Meesles.Avalon.Sim.Assets;
using xpTURN.Klotho.Deterministic.Math;
using Xunit;

namespace Meesles.Avalon.Sim.Tests;

// The BuffStats mini-grammar on SkillAsset: ';'-separated entries, each either a bare stat name (or
// comma list) on the scalar BuffPercent pair, or "<Stat> <pct|flat> <base> [perRank]". Numbers parse
// through integer math only, so the fixed-point result is identical on both peers.
public class BuffStatsParsingTests {
  [Fact]
  public void ABareName_UsesTheScalarPercentPair() {
    var skill = new SkillAsset(0) {
      BuffStats = "MoveSpeed",
      BuffPercent = Frac(5, 100),
      BuffPercentPerRank = Frac(5, 100)
    };

    var spec = skill.BuffSpecs.Should().ContainSingle().Subject;
    spec.Stat.Should().Be(StatType.MoveSpeed);
    spec.Mode.Should().Be(BuffMode.Percent);
    spec.Base.Should().Be(Frac(5, 100));
    spec.PerRank.Should().Be(Frac(5, 100));
  }

  [Fact]
  public void ACommaList_ExpandsToOneSpecPerName_AllOnTheScalarPair() {
    var skill = new SkillAsset(0) {
      BuffStats = "Armor,MagicResist",
      BuffPercent = Frac(7, 100),
      BuffPercentPerRank = FP64.Zero
    };

    skill.BuffSpecs.Should().HaveCount(2);
    skill.BuffSpecs[0].Stat.Should().Be(StatType.Armor);
    skill.BuffSpecs[1].Stat.Should().Be(StatType.MagicResist);
    skill.BuffSpecs.Should().OnlyContain(s => s.Mode == BuffMode.Percent && s.Base == Frac(7, 100));
  }

  [Fact]
  public void AFullEntry_CarriesItsOwnModeBaseAndPerRank() {
    var skill = new SkillAsset(0) { BuffStats = "BonusAttackSpeed flat 0.10 0.10" };

    var spec = skill.BuffSpecs.Should().ContainSingle().Subject;
    spec.Stat.Should().Be(StatType.BonusAttackSpeed);
    spec.Mode.Should().Be(BuffMode.Flat);
    spec.Base.Should().Be(Frac(10, 100));
    spec.PerRank.Should().Be(Frac(10, 100));
  }

  [Fact]
  public void PerRankIsOptional_AndDefaultsToZero() {
    var skill = new SkillAsset(0) { BuffStats = "MoveSpeed pct 0.20" };

    var spec = skill.BuffSpecs.Should().ContainSingle().Subject;
    spec.Base.Should().Be(Frac(20, 100));
    spec.PerRank.Should().Be(FP64.Zero);
  }

  [Fact]
  public void ANegativeMagnitude_ParsesWithItsSign() {
    var skill = new SkillAsset(0) { BuffStats = "Armor pct -0.20 -0.05" };

    var spec = skill.BuffSpecs.Should().ContainSingle().Subject;
    spec.Base.Should().Be(FP64.Zero - Frac(20, 100));
    spec.PerRank.Should().Be(FP64.Zero - Frac(5, 100));
  }

  [Fact]
  public void AMixedRow_KeepsEveryEntryInOrder() {
    var skill = new SkillAsset(0) {
      BuffStats = "AttackDamage pct 0.75 0.30; BonusAttackSpeed flat 0.30 0.10; " +
                  "MoveSpeed pct 0.20; Armor pct -0.20 -0.05; MagicResist pct -0.20 -0.05"
    };

    skill.BuffSpecs.Should().HaveCount(5);
    skill.BuffSpecs[0].Stat.Should().Be(StatType.AttackDamage);
    skill.BuffSpecs[1].Mode.Should().Be(BuffMode.Flat);
    skill.BuffSpecs[2].PerRank.Should().Be(FP64.Zero);
    skill.BuffSpecs[3].Base.Should().Be(FP64.Zero - Frac(20, 100));
    skill.BuffSpecs[4].Stat.Should().Be(StatType.MagicResist);
  }

  [Fact]
  public void AnEmptyString_ParsesToNoSpecs() {
    new SkillAsset(0) { BuffStats = null }.BuffSpecs.Should().BeEmpty();
    new SkillAsset(0) { BuffStats = "  " }.BuffSpecs.Should().BeEmpty();
  }

  [Fact]
  public void MagnitudeAtRank_RampsFromBaseByPerRank() {
    var spec = new SkillAsset(0) { BuffStats = "MoveSpeed pct 0.15 0.05" }.BuffSpecs[0];

    spec.MagnitudeAtRank(0).Should().Be(FP64.Zero);
    spec.MagnitudeAtRank(1).Should().Be(Frac(15, 100));
    spec.MagnitudeAtRank(3).Should().Be(Frac(15, 100) + Frac(5, 100) * FP64.FromInt(2));
  }

  private static FP64 Frac(int numerator, int denominator) {
    return FP64.FromInt(numerator) / FP64.FromInt(denominator);
  }
}
