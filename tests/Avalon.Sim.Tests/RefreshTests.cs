using System.Linq;
using FluentAssertions;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using Meesles.Avalon.Sim.Heroes;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using Xunit;

namespace Meesles.Avalon.Sim.Tests;

// Pickle Knight's Tertiary: the first heal, and the first self-cast row. The heal is a fraction of the
// caster's own MaxHealth so it keeps its meaning as the pool grows with level, and the SelfCast flag is
// what makes the aim point the client sent irrelevant on both ends.
//
// The cleanse half of the skill is not written yet - nothing in the sim applies a negative status to
// clear. See the TODO on CastRefresh.
public class RefreshTests {
  private const int CasterPlayerId = 1;
  private const int Tertiary = (int)SkillSlot.Tertiary;

  [Fact]
  public void Cast_RestoresTheRowsPercentageOfTheCastersMaxHealth() {
    var harness = CreatePickleKnightHarness();
    var skill = RefreshAsset(harness);
    var maxHealth = MaxHealth(harness);
    SetHealth(harness, maxHealth / FP64.FromInt(2));

    LearnAndCast(harness);

    Health(harness).Should().Be(maxHealth / FP64.FromInt(2) + maxHealth * skill.HealPercentAtRank(1));
  }

  // 5% per skill level, off the row: rank 1 is 5% and every rank after is another step of the same.
  [Fact]
  public void EachRank_IsWorthAnotherStepOfTheRowsPercentage() {
    var harness = CreatePickleKnightHarness();
    var skill = RefreshAsset(harness);
    skill.HealPercentAtRank(1).Should().Be(skill.HealPercent);
    skill.HealPercentAtRank(4).Should().Be(skill.HealPercent + skill.HealPercentPerRank * FP64.FromInt(3));

    var maxHealth = MaxHealth(harness);
    SetHealth(harness, FP64.One);

    LearnAndCast(harness, rank: 3);

    Health(harness).Should().Be(FP64.One + maxHealth * skill.HealPercentAtRank(3));
  }

  [Fact]
  public void ItCannotOverheal() {
    var harness = CreatePickleKnightHarness();
    var maxHealth = MaxHealth(harness);
    SetHealth(harness, maxHealth - FP64.One);

    LearnAndCast(harness);

    Health(harness).Should().Be(maxHealth);
  }

  // The cooldown starts before the effect runs, so a cast at full health is spent rather than refunded -
  // the row's full cooldown is the price of pressing it early.
  [Fact]
  public void TheCooldownIsTheRowsAndAWastedCastStillPaysIt() {
    var harness = CreatePickleKnightHarness();
    var skill = RefreshAsset(harness);
    var frame = harness.Frame;
    skill.CooldownMs.Should().BePositive();

    LearnAndCast(harness); // At full health: nothing to restore

    Health(harness).Should().Be(MaxHealth(harness));
    Cooldown(harness).Should().Be(TickMath.MsToTicksCeil(ref frame, skill.CooldownMs) - 1);
  }

  // Self-cast: the aim point never reaches the effect. TryCast replaces it with the caster's own
  // position, so the cast event a view reads points at the hero rather than at the cursor.
  [Fact]
  public void ACastAimedAcrossTheMap_StillResolvesOnTheCaster() {
    var harness = CreatePickleKnightHarness();
    RefreshAsset(harness).IsSelfCast.Should().BeTrue("otherwise this proves nothing");
    var origin = HeroPosition(harness);
    SetHealth(harness, FP64.One);

    harness.Tick(SimHarness.UpgradeSkillCommand(CasterPlayerId, 0, Tertiary));
    var collector = new EventCollector();
    collector.BeginTick(harness.Frame.Tick);
    harness.Frame.EventRaiser = collector;
    harness.Tick(SimHarness.CastSkillCommand(CasterPlayerId, 1, Tertiary,
      origin.x + FP64.FromInt(400), origin.z + FP64.FromInt(400)));

    var cast = collector.Collected.OfType<SkillCastEvent>().Single();
    cast.TargetPosition.Should().Be(new FPVector3(origin.x, origin.y, origin.z));
    Health(harness).Should().BeGreaterThan(FP64.One);
  }

  // A dead hero is refused before the heal runs, so Refresh is never a self-resurrect.
  [Fact]
  public void ADeadHero_CannotCastIt() {
    var harness = CreatePickleKnightHarness();
    harness.Tick(SimHarness.UpgradeSkillCommand(CasterPlayerId, 0, Tertiary));
    SetHealth(harness, FP64.Zero);

    var frame = harness.Frame;
    SkillActions.TryCast(ref frame, CasterPlayerId, Tertiary, HeroPosition(harness)).Should().BeFalse();
    Health(harness).Should().Be(FP64.Zero);
  }

  // --- helpers ---

  // The harness defaults every player to Hairy Wizards, so go through the real faction-select path to
  // get a Pickle Knight on the board.
  private static SimHarness CreatePickleKnightHarness() {
    var harness = SimHarness.CreateInitialized(spawnHeroesNow: false);
    harness.Tick(
      SimHarness.SelectFactionCommand(1, 0, AssetIds.FactionPickleKnights),
      SimHarness.SelectFactionCommand(2, 0, AssetIds.FactionPickleKnights));

    return harness;
  }

  private static void LearnAndCast(SimHarness harness, int rank = 1) {
    var frame = harness.Frame;
    var hero = harness.FindHero(CasterPlayerId);
    frame.Get<Skills>(hero).SkillPoints += rank; // A level-1 hero only carries one
    for (var i = 0; i < rank; i++)
      frame.Get<Skills>(hero).TrySpendPoint(Tertiary, 4).Should().BeTrue();

    harness.Tick(SimHarness.CastSkillCommand(CasterPlayerId, 0, Tertiary));
  }

  private static SkillAsset RefreshAsset(SimHarness harness) {
    return harness.AssetRegistry.Get<SkillAsset>(AssetIds.SkillPickleKnightTertiary);
  }

  private static void SetHealth(SimHarness harness, FP64 current) {
    harness.Frame.Get<Health>(harness.FindHero(CasterPlayerId)).Current = current;
  }

  private static FP64 Health(SimHarness harness) {
    return harness.Frame.GetReadOnly<Health>(harness.FindHero(CasterPlayerId)).Current;
  }

  private static FP64 MaxHealth(SimHarness harness) {
    return harness.Frame.GetReadOnly<Stats>(harness.FindHero(CasterPlayerId)).MaxHealth;
  }

  private static int Cooldown(SimHarness harness) {
    return harness.Frame.GetReadOnly<Skills>(harness.FindHero(CasterPlayerId))
      .GetCooldownRemainingTicks(Tertiary);
  }

  private static FPVector3 HeroPosition(SimHarness harness) {
    return harness.Frame.GetReadOnly<TransformComponent>(harness.FindHero(CasterPlayerId)).Position;
  }
}
