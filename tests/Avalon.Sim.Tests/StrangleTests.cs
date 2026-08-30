using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using Meesles.Avalon.Sim.Factories;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using Xunit;

namespace Meesles.Avalon.Sim.Tests;

// Hairy Wizard's Secondary: a skillshot that deals nothing on contact and instead leaves a MoveSpeed
// slow and a magic-damage burn on the first enemy it touches. The projectile lifecycle itself is
// CrystalBulletsTests' and HairballTests' to own; what is checked here is the on-hit debuff block -
// that both effects land, trace back to the row, and end on their authored clocks - and the burn
// accrual, which is net-new plumbing shared with Bad Hair Day.
//
// Targets are hand-built so they hold still: a real minion carries a NavAgentComponent and would steer
// off the bolt's line mid-flight. They carry exactly what the hit and debuff paths read.
public class StrangleTests {
  private const int CasterPlayerId = 1;
  private const int EnemyTeamId = 2;
  private const int Secondary = (int)SkillSlot.Secondary;
  private const int DummySpeed = 10;

  [Fact]
  public void Cast_PutsOneBoltOnTheAimLineThatDealsNothingOnContact() {
    var harness = CreateHarness();

    var origin = LearnAndCastAlongX(harness);
    var enemy = SpawnDummy(harness, origin + FPVector3.Right * FP64.FromInt(5), EnemyTeamId);
    var healthBefore = harness.Frame.GetReadOnly<Health>(enemy).Current;

    var shots = Projectiles(harness);
    shots.Should().ContainSingle();
    shots[0].Component.Damage.Should().Be(FP64.Zero);
    shots[0].Component.SkillAssetId.Should().Be(AssetIds.SkillHairyWizardSecondary);
    shots[0].Component.Rank.Should().Be(1);

    AdvanceUntilHit(harness);
    // All debuff: the contact takes no health. The burn does, but its first instalment is a second out.
    harness.Frame.GetReadOnly<Health>(enemy).Current.Should().Be(healthBefore);
  }

  [Fact]
  public void AnEnemyItHits_IsSlowedByTheRowsPercentageOfItsMoveSpeed() {
    var harness = CreateHarness();
    var skill = StrangleAsset(harness);

    var origin = LearnAndCastAlongX(harness);
    var enemy = SpawnDummy(harness, origin + FPVector3.Right * FP64.FromInt(5), EnemyTeamId);
    AdvanceUntilHit(harness);

    var percent = skill.BuffSpecs.Single(s => s.Stat == StatType.MoveSpeed).MagnitudeAtRank(1);
    percent.Should().BeLessThan(FP64.Zero, "Strangle slows rather than hastens");
    StatOf(harness, enemy, StatType.MoveSpeed)
      .Should().Be(FP64.FromInt(DummySpeed) + FP64.FromInt(DummySpeed) * percent);

    var entry = Entries(harness, enemy).Should().ContainSingle().Subject;
    entry.SourceId.Should().Be(AssetIds.SkillHairyWizardSecondary);
    entry.Stat.Should().Be(StatType.MoveSpeed);
  }

  [Fact]
  public void AnEnemyItHits_BurnsForTheRowsRateOverItsWindow() {
    var harness = CreateHarness();
    var skill = StrangleAsset(harness);
    var burnTicks = MsToTicks(harness, skill.DotDurationMs);

    var origin = LearnAndCastAlongX(harness);
    var enemy = SpawnDummy(harness, origin + FPVector3.Right * FP64.FromInt(5), EnemyTeamId);
    var healthBefore = harness.Frame.GetReadOnly<Health>(enemy).Current;

    var hitTick = AdvanceUntilHit(harness);
    AdvancePast(harness, hitTick + burnTicks);

    // The payout interval slices the window but not the total: the burn accrues every tick the burn
    // was active (the hit tick excluded) and pays the accrued whole out in instalments, so the sum is
    // the same floored window total either way.
    var expected = ExpectedBurnTotal(skill.DotDamagePerSecondAtRank(1), burnTicks - 1);
    expected.Should().BeGreaterThan(FP64.Zero);
    (healthBefore - harness.Frame.GetReadOnly<Health>(enemy).Current).Should().Be(expected);
  }

  [Fact]
  public void TheBurn_LandsInInstalmentsOnThePayoutInterval_NotEveryTick() {
    var harness = CreateHarness();
    var skill = StrangleAsset(harness);
    var burnTicks = MsToTicks(harness, skill.DotDurationMs);
    var intervalTicks = MsToTicks(harness, DamageOverTimes.PayoutIntervalMs);
    var perSecond = skill.DotDamagePerSecondAtRank(1);

    var origin = LearnAndCastAlongX(harness);
    var enemy = SpawnDummy(harness, origin + FPVector3.Right * FP64.FromInt(5), EnemyTeamId);
    var hitTick = AdvanceUntilHit(harness);

    var instalments = new List<(int Tick, FP64 Amount)>();
    var last = harness.Frame.GetReadOnly<Health>(enemy).Current;
    while (harness.Frame.Tick <= hitTick + burnTicks) {
      harness.Tick();
      var current = harness.Frame.GetReadOnly<Health>(enemy).Current;
      if (current < last)
        instalments.Add((harness.Frame.Tick, last - current));
      last = current;
    }

    // A handful of solid hits, not ~12 floored ones: one per interval over the window, plus the
    // expiry instalment for the tail.
    instalments.Count.Should().BeLessThanOrEqualTo(burnTicks / intervalTicks + 1);
    instalments.Count.Should().BeGreaterThan(1);
    instalments.Should().OnlyContain(i => i.Amount >= perSecond / FP64.FromInt(2),
      "each instalment is roughly a second's worth of the rate, not a floored 1");
    for (var i = 1; i < instalments.Count; i++)
      (instalments[i].Tick - instalments[i - 1].Tick).Should().BeGreaterThanOrEqualTo(intervalTicks - 1);
    instalments.Aggregate(FP64.Zero, (sum, i) => sum + i.Amount)
      .Should().Be(ExpectedBurnTotal(perSecond, burnTicks - 1));
  }

  [Fact]
  public void TheBurnAndTheSlowEndOnTheirAuthoredClocks() {
    var harness = CreateHarness();
    var skill = StrangleAsset(harness);
    var burnTicks = MsToTicks(harness, skill.DotDurationMs);
    var slowTicks = MsToTicks(harness, skill.BuffDurationMsAtRank(1));

    var origin = LearnAndCastAlongX(harness);
    var enemy = SpawnDummy(harness, origin + FPVector3.Right * FP64.FromInt(5), EnemyTeamId);
    var hitTick = AdvanceUntilHit(harness);

    AdvancePast(harness, hitTick + System.Math.Max(burnTicks, slowTicks));

    IsBurning(harness, enemy).Should().BeFalse();
    harness.Frame.Has<DamageOverTime>(enemy)
      .Should().BeTrue("the burn slot is cleared in place, not removed");
    // The slow reverts exactly: the dummy is back to the speed it had before the bolt.
    StatOf(harness, enemy, StatType.MoveSpeed).Should().Be(FP64.FromInt(DummySpeed));
    Entries(harness, enemy).Should().BeEmpty();
  }

  [Fact]
  public void EachRank_DeepensBothTheSlowAndTheBurn() {
    var harness = CreateHarness();
    var skill = StrangleAsset(harness);

    skill.DotDurationMs.Should().BePositive();
    skill.DotDamagePerSecond.Should().BeGreaterThan(FP64.Zero);
    skill.DotDamagePerSecondAtRank(2).Should().Be(skill.DotDamagePerSecond + skill.DotDamagePerSecondPerRank);
    var slow = skill.BuffSpecs.Single(s => s.Stat == StatType.MoveSpeed);
    slow.MagnitudeAtRank(2).Should().Be(slow.Base + slow.PerRank);

    var origin = LearnAndCastAlongX(harness, rank: 2);
    var enemy = SpawnDummy(harness, origin + FPVector3.Right * FP64.FromInt(5), EnemyTeamId);
    AdvanceUntilHit(harness);

    StatOf(harness, enemy, StatType.MoveSpeed)
      .Should().Be(FP64.FromInt(DummySpeed) + FP64.FromInt(DummySpeed) * slow.MagnitudeAtRank(2));
  }

  [Fact]
  public void ABoltThatMisses_LeavesNoSlowOrBurn() {
    var harness = CreateHarness();

    var origin = LearnAndCastAlongX(harness);
    // Well off the aim line: the bolt sails past and expires at range.
    var bystander = SpawnDummy(harness,
      origin + FPVector3.Right * FP64.FromInt(5) + FPVector3.Forward * FP64.FromInt(10), EnemyTeamId);
    var healthBefore = harness.Frame.GetReadOnly<Health>(bystander).Current;

    for (var i = 0; i < 60; i++)
      harness.Tick();

    harness.Count<Projectile>().Should().Be(0);
    StatOf(harness, bystander, StatType.MoveSpeed).Should().Be(FP64.FromInt(DummySpeed));
    harness.Frame.GetReadOnly<Health>(bystander).Current.Should().Be(healthBefore);
    IsBurning(harness, bystander).Should().BeFalse();
  }

  [Fact]
  public void ALethalBurn_CreditsTheCasterSoTheKillPaysXp() {
    var harness = CreateHarness();
    var xpBefore = harness.Frame
      .GetReadOnly<Experience>(harness.FindHero(CasterPlayerId)).Xp;
    var expectedXp = harness.AssetRegistry.Get<XpRulesAsset>().XpPerMinionKill;

    var origin = LearnAndCastAlongX(harness);
    var enemy = SpawnDummy(harness, origin + FPVector3.Right * FP64.FromInt(5), EnemyTeamId);
    harness.Frame.Get<Health>(enemy).Current = FP64.FromInt(2);

    var hitTick = AdvanceUntilHit(harness);
    AdvancePast(harness, hitTick + 120);

    // Kill credit rides Health.LastDamagerUnitId, which the burn's ApplyDamage sets to the caster.
    harness.Frame.GetReadOnly<Experience>(harness.FindHero(CasterPlayerId))
      .Xp.Should().Be(xpBefore + expectedXp);
  }

  [Fact]
  public void TheBurnIsClearedWhenItsTargetDies() {
    var harness = CreateHarness();
    var skill = StrangleAsset(harness);
    var target = harness.FindHero(2);

    var frame = harness.Frame;
    DamageOverTimes.Apply(ref frame, target, harness.FindHero(CasterPlayerId),
      AssetIds.SkillHairyWizardSecondary, skill.DotDamagePerSecondAtRank(1), 200).Should().BeTrue();
    IsBurning(harness, target).Should().BeTrue();

    harness.Frame.Get<Health>(target).Current = FP64.Zero;
    harness.Tick(); // RespawnSystem.BeginRespawn runs ClearActiveState

    IsBurning(harness, target).Should().BeFalse();
  }

  // --- setup helpers ---

  // The harness defaults every player to Hairy Wizards, so Strangle is already in the Secondary slot.
  // Stripping Combat and holding the first wave off the board leave the burn as the only thing that
  // can move a target's health bar over the three seconds these tests run - a delta means one thing.
  private static SimHarness CreateHarness() {
    var harness = SimHarness.CreateInitialized();
    harness.AssetRegistry.Get<WaveRulesAsset>().FirstWaveDelayTicks = 1_000_000;

    var frame = harness.Frame;
    var attackers = new List<EntityRef>();

    var filter = frame.Filter<Combat>();
    while (filter.Next(out var entity))
      attackers.Add(entity);

    foreach (var entity in attackers)
      frame.Remove<Combat>(entity);

    return harness;
  }

  // Learns the slot to `rank`, then casts along +X. Returns the caster position the bolt fired from,
  // read after the points are spent - past the tick NavigationAgentSystem snaps the hero onto the mesh.
  private static FPVector3 LearnAndCastAlongX(SimHarness harness, int rank = 1) {
    var frame = harness.Frame;
    var hero = harness.FindHero(CasterPlayerId);
    frame.Get<Skills>(hero).SkillPoints += rank; // A level-1 hero only carries one
    for (var i = 0; i < rank; i++)
      frame.Get<Skills>(hero).TrySpendPoint(Secondary, 4).Should().BeTrue();

    harness.Tick(); // let NavigationAgentSystem snap the hero onto the mesh before we read its position

    var origin = HeroPosition(harness);
    harness.Tick(SimHarness.CastSkillCommand(CasterPlayerId, harness.Frame.Tick, Secondary,
      origin.x + FP64.FromInt(20), origin.z));
    return origin;
  }

  // Ticks until the bolt is gone, returning the tick it resolved on. The hit lands in ProjectileSystem
  // after TimedEffectSystem has already run that tick, so accrual starts the tick after and the first
  // instalment lands one payout interval later.
  private static int AdvanceUntilHit(SimHarness harness) {
    harness.Count<Projectile>().Should().Be(1, "a bolt is in the air to resolve");

    var tick = harness.Frame.Tick;
    while (harness.Count<Projectile>() > 0) {
      tick = harness.Frame.Tick;
      harness.Tick();
    }

    return tick;
  }

  private static void AdvancePast(SimHarness harness, int tick) {
    while (harness.Frame.Tick <= tick)
      harness.Tick();
  }

  private static EntityRef SpawnDummy(SimHarness harness, FPVector3 position, int teamId) {
    var frame = harness.Frame;
    var entity = frame.CreateEntity();

    frame.Add(entity, TransformFactory.At(position));
    frame.Add(entity, new UnitIdentity {
      UnitId = UnitLookup.NextUnitId(ref frame),
      UnitTypeId = SimulationSetup.MinionUnitTypeId
    });
    frame.Add(entity, new Team(teamId));
    frame.Add(entity, new Health(500));
    frame.Add(entity, new Minion { WaveId = 0 });
    frame.Add(entity, Stats.Create().With(StatType.MoveSpeed, FP64.FromInt(DummySpeed)));

    return entity;
  }

  // The floored window total DamageOverTimes pays across `activeTicks` of accrual - a per-tick amount
  // fixed at attach time, summed and floored. The payout interval changes when it lands, not this sum,
  // so the fixed-point figure is bit-identical rather than an approximation.
  private static FP64 ExpectedBurnTotal(FP64 damagePerSecond, int activeTicks) {
    var perTick = damagePerSecond * FP64.FromInt(SimHarness.DefaultDeltaTimeMs) / FP64.FromInt(1000);
    var pending = FP64.Zero;
    var total = FP64.Zero;
    for (var i = 0; i < activeTicks; i++) {
      pending += perTick;
      var whole = FP64.Floor(pending);
      if (whole >= FP64.One) {
        pending -= whole;
        total += whole;
      }
    }

    return total;
  }

  // --- readers ---

  private static SkillAsset StrangleAsset(SimHarness harness) {
    return harness.AssetRegistry.Get<SkillAsset>(AssetIds.SkillHairyWizardSecondary);
  }

  private static int MsToTicks(SimHarness harness, int milliseconds) {
    var frame = harness.Frame;
    return TickMath.MsToTicksCeil(ref frame, milliseconds);
  }

  private static bool IsBurning(SimHarness harness, EntityRef entity) {
    var frame = harness.Frame;
    return DamageOverTimes.IsBurning(ref frame, entity);
  }

  private static FPVector3 HeroPosition(SimHarness harness) {
    return harness.Frame.GetReadOnly<TransformComponent>(harness.FindHero(CasterPlayerId)).Position;
  }

  private static FP64 StatOf(SimHarness harness, EntityRef entity, StatType stat) {
    return harness.Frame.GetReadOnly<Stats>(entity).Get(stat);
  }

  private static List<(Projectile Component, FPVector3 Position)> Projectiles(SimHarness harness) {
    var frame = harness.Frame;
    var found = new List<(Projectile, FPVector3)>();

    var filter = frame.Filter<Projectile, TransformComponent>();
    while (filter.Next(out var entity))
      found.Add((frame.GetReadOnly<Projectile>(entity), frame.GetReadOnly<TransformComponent>(entity).Position));

    return found.OrderBy(p => p.Item1.Index).ToList();
  }

  private readonly record struct BuffEntry(int SourceId, StatType Stat, FP64 Applied, int ExpiryTick);

  private static List<BuffEntry> Entries(SimHarness harness, EntityRef entity) {
    var entries = new List<BuffEntry>();
    if (!harness.Frame.Has<StatBuffs>(entity))
      return entries;

    ref readonly var buffs = ref harness.Frame.GetReadOnly<StatBuffs>(entity);
    for (var i = 0; i < StatBuffs.MaxEntries; i++)
      if (buffs.IsActive(i))
        entries.Add(new BuffEntry(buffs.GetSourceId(i), buffs.GetStat(i), buffs.GetApplied(i),
          buffs.GetExpiryTick(i)));

    return entries;
  }
}
