using System.Linq;
using FluentAssertions;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using Xunit;

namespace Meesles.Avalon.Sim.Tests;

// Pickle Knight's Tertiary: the first heal, and the first self-cast row. The heal is a fraction of the
// caster's own MaxHealth so it keeps its meaning as the pool grows with level, and the SelfCast flag is
// what makes the aim point the client sent irrelevant on both ends.
//
// It is also the first cleanse: the row's ClearsDebuffs flag routes through BuffsController.ClearNegative,
// which strips every negative status the caster carries - an adverse StatBuffs entry, a snare, a
// silence, a burn - and leaves the beneficial ones running.
public class RefreshTests {
  private const int CasterPlayerId = 1;
  private const int Tertiary = (int)SkillSlot.Tertiary;
  private const int SomeDebuffSource = 4242; // A source id that is not a real skill row

  [Fact]
  public void Cast_RestoresTheRowsPercentageOfTheCastersMaxHealth() {
    var harness = CreatePickleKnightHarness();
    var skill = RefreshAsset(harness);
    var maxHealth = MaxHealth(harness);
    SetHealth(harness, maxHealth / FP64.FromInt(2));

    LearnAndCast(harness);

    Health(harness).Should().Be(maxHealth / FP64.FromInt(2) + maxHealth * SkillAsset.AtRank(skill.HealPercent, skill.HealPercentPerRank, 1));
  }

  // 5% per skill level, off the row: rank 1 is 5% and every rank after is another step of the same.
  [Fact]
  public void EachRank_IsWorthAnotherStepOfTheRowsPercentage() {
    var harness = CreatePickleKnightHarness();
    var skill = RefreshAsset(harness);
    SkillAsset.AtRank(skill.HealPercent, skill.HealPercentPerRank, 1).Should().Be(skill.HealPercent);
    SkillAsset.AtRank(skill.HealPercent, skill.HealPercentPerRank, 4).Should().Be(skill.HealPercent + skill.HealPercentPerRank * FP64.FromInt(3));

    var maxHealth = MaxHealth(harness);
    SetHealth(harness, FP64.One);

    LearnAndCast(harness, rank: 3);

    Health(harness).Should().Be(FP64.One + maxHealth * SkillAsset.AtRank(skill.HealPercent, skill.HealPercentPerRank, 3));
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
    RefreshAsset(harness).SelfCast.Should().BeTrue("otherwise this proves nothing");
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
    SkillsController.TryCast(ref frame, CasterPlayerId, Tertiary, HeroPosition(harness)).Should().BeFalse();
    Health(harness).Should().Be(FP64.Zero);
  }

  [Fact]
  public void TheCleanseIsAuthoredOnTheRow() {
    RefreshAsset(CreatePickleKnightHarness()).ClearsDebuffs.Should().BeTrue();
  }

  // One cast takes off every kind of negative status at once: the signed MoveSpeed slow reverts, and
  // the snare and burn clear. Silence is the one it cannot answer - see the catch-22 test below.
  [Fact]
  public void Cast_StripsEveryNegativeStatusTheCasterCarries() {
    var harness = CreatePickleKnightHarness();
    var hero = harness.FindHero(CasterPlayerId);
    var baseSpeed = CasterStat(harness, StatType.MoveSpeed);

    var frame = harness.Frame;
    BuffsController.Apply(ref frame, hero, SomeDebuffSource, StatType.MoveSpeed,
      FP64.Zero, -FP64.Half, 600).Should().BeTrue();
    SnareController.Apply(ref frame, hero, SomeDebuffSource, 600).Should().BeTrue();
    DamageOverTimeController.Apply(ref frame, hero, hero, SomeDebuffSource, FP64.FromInt(10), 600).Should().BeTrue();

    LearnAndCast(harness);

    var after = harness.Frame;
    SnareController.IsSnared(ref after, hero).Should().BeFalse();
    DamageOverTimeController.IsBurning(ref after, hero).Should().BeFalse();
    CasterStat(harness, StatType.MoveSpeed).Should().Be(baseSpeed);
    ActiveBuffCount(harness).Should().Be(0);
  }

  // Refresh is self-cast, and a silenced hero cannot cast: the one cleanse it can never deliver to
  // itself is the one against a silence. BuffsController.ClearNegative still strips it - so an ally-cast
  // cleanse would - but the hero pressing its own button will not.
  [Fact]
  public void ASilencedCaster_CannotCastRefreshToCleanseItself() {
    var harness = CreatePickleKnightHarness();
    var hero = harness.FindHero(CasterPlayerId);
    harness.Tick(SimHarness.UpgradeSkillCommand(CasterPlayerId, 0, Tertiary));

    var frame = harness.Frame;
    SilenceController.Apply(ref frame, hero, SomeDebuffSource, 600).Should().BeTrue();

    SkillsController.CanCast(ref frame, CasterPlayerId, Tertiary).Should().BeFalse();
    SkillsController.TryCast(ref frame, CasterPlayerId, Tertiary, HeroPosition(harness)).Should().BeFalse();

    BuffsController.ClearNegative(ref frame, hero);
    SilenceController.IsSilenced(ref frame, hero).Should().BeFalse();
  }

  // The cleanse tells a slow from a haste by which way the stat moved, so a running buff survives it.
  [Fact]
  public void Cast_LeavesABeneficialBuffRunning() {
    var harness = CreatePickleKnightHarness();
    var hero = harness.FindHero(CasterPlayerId);
    var baseArmor = CasterStat(harness, StatType.Armor);
    var baseSpeed = CasterStat(harness, StatType.MoveSpeed);

    var frame = harness.Frame;
    BuffsController.Apply(ref frame, hero, SomeDebuffSource, StatType.Armor,
      FP64.Zero, FP64.Half, 600).Should().BeTrue();
    BuffsController.Apply(ref frame, hero, SomeDebuffSource, StatType.MoveSpeed,
      FP64.Zero, -FP64.Half, 600).Should().BeTrue();

    LearnAndCast(harness);

    CasterStat(harness, StatType.MoveSpeed).Should().Be(baseSpeed, "the slow is gone");
    CasterStat(harness, StatType.Armor).Should().Be(baseArmor + baseArmor * FP64.Half, "the buff stays");
    ActiveBuffCount(harness).Should().Be(1);
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

  private static FP64 CasterStat(SimHarness harness, StatType stat) {
    return harness.Frame.GetReadOnly<Stats>(harness.FindHero(CasterPlayerId)).Get(stat);
  }

  private static int ActiveBuffCount(SimHarness harness) {
    var frame = harness.Frame;
    var hero = harness.FindHero(CasterPlayerId);
    if (!frame.Has<StatBuffs>(hero))
      return 0;

    ref readonly var buffs = ref frame.GetReadOnly<StatBuffs>(hero);
    var count = 0;
    for (var i = 0; i < StatBuffs.MaxEntries; i++)
      if (buffs.IsActive(i))
        count++;
    return count;
  }

  private static int Cooldown(SimHarness harness) {
    return harness.Frame.GetReadOnly<Skills>(harness.FindHero(CasterPlayerId))
      .GetCooldownRemainingTicks(Tertiary);
  }

  private static FPVector3 HeroPosition(SimHarness harness) {
    return harness.Frame.GetReadOnly<TransformComponent>(harness.FindHero(CasterPlayerId)).Position;
  }
}
