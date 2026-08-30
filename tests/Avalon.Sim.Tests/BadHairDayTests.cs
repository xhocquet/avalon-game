using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using Meesles.Avalon.Sim.Factories;
using Meesles.Avalon.Sim.Heroes;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using Xunit;

namespace Meesles.Avalon.Sim.Tests;

// Hairy Wizard's Ultimate: a channel that trails a storm on the caster for the wind-up, pulsing the
// row's per-second rate at every hostile in the disc, then snares whatever it catches when the
// wind-up ends. It reuses the SkillCharges clock and the burst snare Chrysalis owns; what is new
// here is the channel aura - a moving disc re-collected every payout interval, not a lingering
// per-target burn - so that is what these check.
//
// Targets are hand-built minions so they hold still: a real one carries a NavAgentComponent and
// steers around mid-channel. The caster is the real hero because half the point is that it moves.
public class BadHairDayTests {
  private const int CasterPlayerId = 1;
  private const int EnemyPlayerId = 2;
  private const int CasterTeamId = 1;
  private const int EnemyTeamId = 2;
  private const int Ultimate = (int)SkillSlot.Ultimate;

  [Fact]
  public void Cast_TrailsAStormThatPulsesTheDiscThenSnaresWhenTheWindUpEnds() {
    var harness = CreateHarness();
    var skill = BadHairDayAsset(harness);

    var castTick = LearnAndCast(harness);
    var enemy = SpawnDummy(harness, HeroPosition(harness), EnemyTeamId, isMinion: true);
    var healthBefore = Health(harness, enemy);

    // Standing in it: chipped down over the wind-up, and not yet snared right up to the last tick.
    AdvanceTo(harness, castTick + ChargeTicks(harness, skill) - 1);
    Health(harness, enemy).Should().BeLessThan(healthBefore);
    IsSnared(harness, enemy).Should().BeFalse();
    IsCharging(harness).Should().BeTrue();

    AdvanceTo(harness, castTick + ChargeTicks(harness, skill));
    IsSnared(harness, enemy).Should().BeTrue();
    IsCharging(harness).Should().BeFalse();
  }

  // The whole skill is one row: the wind-up, the disc, the per-second rate, the hold. Nothing is
  // hardcoded here and nothing about it is a burst on contact - the burst carries no damage.
  [Fact]
  public void EveryNumber_TracesBackToTheAssetRow() {
    var harness = CreateHarness();
    var skill = BadHairDayAsset(harness);

    skill.IsSelfCast.Should().BeTrue();
    skill.ChargeDurationMs.Should().BePositive();
    skill.ChargeRootsItsCaster.Should().BeFalse("the wizard walks the storm around");
    skill.HasArea.Should().BeTrue();
    skill.DotDamagePerSecond.Should().BeGreaterThan(FP64.Zero);
    skill.SnareDurationMs.Should().BePositive();

    var castTick = LearnAndCast(harness);

    var charge = harness.Frame.GetReadOnly<SkillCharge>(Caster(harness));
    charge.SourceId.Should().Be(AssetIds.SkillHairyWizardUltimate);
    charge.Damage.Should().Be(FP64.Zero, "the burst is snare-only; damage comes from the aura");
    charge.Radius.Should().Be(skill.AreaRadius);
    charge.DetonateTick.Should().Be(castTick + ChargeTicks(harness, skill));
    charge.SnareDurationTicks.Should().Be(Ticks(harness, skill.SnareDurationMsAtRank(1)));
    charge.HasAura.Should().BeTrue();
    charge.AuraIntervalTicks.Should().Be(Ticks(harness, DamageOverTimes.PayoutIntervalMs));
    charge.AuraAccrualPerTick.Should().Be(
      skill.DotDamagePerSecondAtRank(1) * FP64.FromInt(SimHarness.DefaultDeltaTimeMs) / FP64.FromInt(1000));
  }

  [Fact]
  public void TheChannelTotal_IsTheRateAcrossTheWindUp() {
    var harness = CreateHarness();
    var skill = BadHairDayAsset(harness);

    var castTick = LearnAndCast(harness);
    var enemy = SpawnDummy(harness, HeroPosition(harness), EnemyTeamId, isMinion: true);
    var healthBefore = Health(harness, enemy);

    AdvanceTo(harness, castTick + ChargeTicks(harness, skill));

    var expected = ExpectedChannelTotal(skill.DotDamagePerSecondAtRank(1), ChargeTicks(harness, skill));
    expected.Should().BeGreaterThan(FP64.Zero);
    (healthBefore - Health(harness, enemy)).Should().Be(expected);
  }

  [Fact]
  public void ThePulsesLandInInstalments_NotEveryTick() {
    var harness = CreateHarness();
    var skill = BadHairDayAsset(harness);
    var chargeTicks = ChargeTicks(harness, skill);
    var intervalTicks = Ticks(harness, DamageOverTimes.PayoutIntervalMs);
    var perSecond = skill.DotDamagePerSecondAtRank(1);

    var castTick = LearnAndCast(harness);
    var enemy = SpawnDummy(harness, HeroPosition(harness), EnemyTeamId, isMinion: true);

    var instalments = new List<(int Tick, FP64 Amount)>();
    var last = Health(harness, enemy);
    while (harness.Frame.Tick <= castTick + chargeTicks) {
      Tick(harness);
      var current = Health(harness, enemy);
      if (current < last)
        instalments.Add((harness.Frame.Tick, last - current));
      last = current;
    }

    // A handful of solid hits, not ~190 floored ones: one per interval over the wind-up plus the
    // tail paid as the burst goes off.
    instalments.Count.Should().BeLessThanOrEqualTo(chargeTicks / intervalTicks + 1);
    instalments.Count.Should().BeGreaterThan(1);
    instalments.Should().OnlyContain(i => i.Amount >= perSecond / FP64.FromInt(2),
      "each instalment is roughly a second's worth of the rate, not a floored 1");
    for (var i = 1; i < instalments.Count; i++)
      (instalments[i].Tick - instalments[i - 1].Tick).Should().BeGreaterThanOrEqualTo(intervalTicks - 1);
    instalments.Aggregate(FP64.Zero, (sum, i) => sum + i.Amount)
      .Should().Be(ExpectedChannelTotal(perSecond, chargeTicks));
  }

  [Fact]
  public void TheStormFollowsTheWizard_WhoIsNotRooted() {
    var harness = CreateHarness();
    var skill = BadHairDayAsset(harness);
    var full = ExpectedChannelTotal(skill.DotDamagePerSecondAtRank(1), ChargeTicks(harness, skill));

    var castTick = LearnAndCast(harness);
    var origin = HeroPosition(harness);

    var leftBehind = SpawnDummy(harness, origin, EnemyTeamId, isMinion: true);
    var walkedInto = SpawnDummy(harness, Offset(origin, FP64.FromInt(10)), EnemyTeamId, isMinion: true);
    var leftHealth = Health(harness, leftBehind);
    var aheadHealth = Health(harness, walkedInto);

    Tick(harness, SimHarness.MoveCommand(CasterPlayerId, 0, origin.x + FP64.FromInt(12), origin.z));
    AdvanceTo(harness, castTick + ChargeTicks(harness, skill));

    HeroPosition(harness).x.Should().BeGreaterThan(origin.x + FP64.FromInt(5), "the channel roots nothing");

    // Storm walked off the one it started on and onto the one ahead: the straggler took only the
    // early pulses and was not there for the snare, the one downrange took the late ones and was.
    var leftTook = leftHealth - Health(harness, leftBehind);
    var aheadTook = aheadHealth - Health(harness, walkedInto);
    leftTook.Should().BeGreaterThan(FP64.Zero);
    leftTook.Should().BeLessThan(full);
    IsSnared(harness, leftBehind).Should().BeFalse();
    aheadTook.Should().BeGreaterThan(FP64.Zero);
    IsSnared(harness, walkedInto).Should().BeTrue();
  }

  [Fact]
  public void OnlyHostilesInsideTheDiscArePulsed() {
    var harness = CreateHarness();
    var skill = BadHairDayAsset(harness);

    var castTick = LearnAndCast(harness);
    var origin = HeroPosition(harness);

    var inside = SpawnDummy(harness, Offset(origin, skill.AreaRadius - FP64.One), EnemyTeamId, isMinion: true);
    var outside = SpawnDummy(harness, Offset(origin, skill.AreaRadius + FP64.FromInt(3)), EnemyTeamId, isMinion: true);
    var friendly = SpawnDummy(harness, origin, CasterTeamId, isMinion: true);
    var structure = SpawnDummy(harness, origin, EnemyTeamId, isMinion: false);
    var before = new[] { inside, outside, friendly, structure }.Select(e => Health(harness, e)).ToList();

    AdvanceTo(harness, castTick + ChargeTicks(harness, skill));

    Health(harness, inside).Should().BeLessThan(before[0]);
    IsSnared(harness, inside).Should().BeTrue();
    Health(harness, outside).Should().Be(before[1]);
    Health(harness, friendly).Should().Be(before[2]);
    Health(harness, structure).Should().Be(before[3], "structures are excluded from skill hits");
  }

  [Fact]
  public void DyingMidChannel_EndsThePulsesAndFiresNoSnare() {
    var harness = CreateHarness();
    var skill = BadHairDayAsset(harness);

    var castTick = LearnAndCast(harness);
    var enemy = SpawnDummy(harness, HeroPosition(harness), EnemyTeamId, isMinion: true);
    var healthBefore = Health(harness, enemy);

    AdvanceTo(harness, castTick + Ticks(harness, DamageOverTimes.PayoutIntervalMs));
    var healthAtDeath = Health(harness, enemy);
    healthAtDeath.Should().BeLessThan(healthBefore, "one pulse landed before the caster died");

    harness.Frame.Get<Health>(Caster(harness)).Current = FP64.Zero;
    Tick(harness); // RespawnSystem picks the death up

    IsCharging(harness).Should().BeFalse();

    AdvanceTo(harness, castTick + ChargeTicks(harness, skill));
    Health(harness, enemy).Should().Be(healthAtDeath, "no pulses after the caster fell");
    IsSnared(harness, enemy).Should().BeFalse();
  }

  [Fact]
  public void EachRank_DeepensThePulse() {
    var harness = CreateHarness();
    var skill = BadHairDayAsset(harness);

    skill.DotDamagePerSecondAtRank(2).Should().Be(skill.DotDamagePerSecond + skill.DotDamagePerSecondPerRank);

    var castTick = LearnAndCast(harness, rank: 2);
    var enemy = SpawnDummy(harness, HeroPosition(harness), EnemyTeamId, isMinion: true);
    var healthBefore = Health(harness, enemy);

    AdvanceTo(harness, castTick + ChargeTicks(harness, skill));

    (healthBefore - Health(harness, enemy))
      .Should().Be(ExpectedChannelTotal(skill.DotDamagePerSecondAtRank(2), ChargeTicks(harness, skill)));
  }

  [Fact]
  public void Detonation_RaisesOneEventForTheSnare() {
    var harness = CreateHarness();
    var skill = BadHairDayAsset(harness);

    var castTick = LearnAndCast(harness);
    SpawnDummy(harness, HeroPosition(harness), EnemyTeamId, isMinion: true);

    var collector = CollectEvents(harness);
    AdvanceTo(harness, castTick + ChargeTicks(harness, skill));

    var detonated = collector.Collected.OfType<SkillChargeDetonatedEvent>().Should().ContainSingle().Subject;
    detonated.SkillAssetId.Should().Be(AssetIds.SkillHairyWizardUltimate);
    detonated.CasterUnitId.Should().Be(UnitId(harness, Caster(harness)));
    detonated.Radius.Should().Be(skill.AreaRadius);
    detonated.HitCount.Should().Be(1);
  }

  // --- setup helpers ---

  private const int DummyHealth = 3000;
  private const int DummySpeed = 10;

  // The harness defaults every player to Hairy Wizards, so Bad Hair Day is already in the Ultimate
  // slot. Stripping Combat and holding the first wave off leave the channel as the only thing that
  // can move a target's health bar.
  private static SimHarness CreateHarness() {
    var harness = SimHarness.CreateInitialized();
    harness.AssetRegistry.Get<WaveRulesAsset>().FirstWaveDelayTicks = 1_000_000;
    StripCombat(harness);
    return harness;
  }

  private static void Tick(SimHarness harness, params ICommand[] commands) {
    harness.Tick(commands);
    StripCombat(harness);
  }

  private static void StripCombat(SimHarness harness) {
    var frame = harness.Frame;
    var attackers = new List<EntityRef>();

    var filter = frame.Filter<Combat>();
    while (filter.Next(out var entity))
      attackers.Add(entity);

    foreach (var entity in attackers)
      frame.Remove<Combat>(entity);
  }

  // Learns the slot to `rank`, then self-casts. Returns the tick the cast executed on, which both the
  // wind-up clock and the aura interval are measured from.
  private static int LearnAndCast(SimHarness harness, int rank = 1) {
    var frame = harness.Frame;
    var hero = harness.FindHero(CasterPlayerId);
    frame.Get<Skills>(hero).SkillPoints += rank;
    for (var i = 0; i < rank; i++)
      frame.Get<Skills>(hero).TrySpendPoint(Ultimate, 4).Should().BeTrue();

    Tick(harness); // let NavigationAgentSystem snap the hero onto the mesh before its position is read

    var castTick = harness.Frame.Tick;
    Tick(harness, SimHarness.CastSkillCommand(CasterPlayerId, castTick, Ultimate));
    return castTick;
  }

  private static void AdvanceTo(SimHarness harness, int tick) {
    while (harness.Frame.Tick <= tick)
      Tick(harness);
  }

  private static EntityRef SpawnDummy(SimHarness harness, FPVector3 position, int teamId, bool isMinion) {
    var frame = harness.Frame;
    var entity = frame.CreateEntity();

    frame.Add(entity, TransformFactory.At(position));
    frame.Add(entity, new UnitIdentity {
      UnitId = UnitLookup.NextUnitId(ref frame),
      UnitTypeId = isMinion ? SimulationSetup.MinionUnitTypeId : SimulationSetup.TurretUnitTypeId
    });
    frame.Add(entity, new Team(teamId));
    frame.Add(entity, new Health(DummyHealth));
    frame.Add(entity, Stats.Create().With(StatType.MoveSpeed, FP64.FromInt(DummySpeed)));

    if (isMinion)
      frame.Add(entity, new Minion { WaveId = 0 });
    else
      frame.Add(entity, new Turret { TurretId = 99 });

    return entity;
  }

  private static EventCollector CollectEvents(SimHarness harness) {
    var collector = new EventCollector();
    collector.BeginTick(harness.Frame.Tick);
    harness.Frame.EventRaiser = collector;
    return collector;
  }

  // The floored window total the aura pays across `activeTicks` of accrual: a per-tick amount fixed
  // at cast time, summed with carry and floored. The payout interval changes when it lands, not this
  // sum, so the fixed-point figure is bit-identical rather than an approximation.
  private static FP64 ExpectedChannelTotal(FP64 damagePerSecond, int activeTicks) {
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

  private static SkillAsset BadHairDayAsset(SimHarness harness) {
    return harness.AssetRegistry.Get<SkillAsset>(AssetIds.SkillHairyWizardUltimate);
  }

  private static bool IsCharging(SimHarness harness) {
    var frame = harness.Frame;
    return SkillCharges.IsCharging(ref frame, Caster(harness));
  }

  private static bool IsSnared(SimHarness harness, EntityRef entity) {
    var frame = harness.Frame;
    return Snares.IsSnared(ref frame, entity);
  }

  private static EntityRef Caster(SimHarness harness) {
    return harness.FindHero(CasterPlayerId);
  }

  private static FPVector3 HeroPosition(SimHarness harness) {
    return Position(harness, Caster(harness));
  }

  private static FPVector3 Position(SimHarness harness, EntityRef entity) {
    return harness.Frame.GetReadOnly<TransformComponent>(entity).Position;
  }

  private static FPVector3 Offset(FPVector3 origin, FP64 distance) {
    return origin + FPVector3.Right * distance;
  }

  private static FP64 Health(SimHarness harness, EntityRef entity) {
    return harness.Frame.GetReadOnly<Health>(entity).Current;
  }

  private static int UnitId(SimHarness harness, EntityRef entity) {
    return harness.Frame.GetReadOnly<UnitIdentity>(entity).UnitId;
  }

  private static int Ticks(SimHarness harness, int milliseconds) {
    var frame = harness.Frame;
    return TickMath.MsToTicksCeil(ref frame, milliseconds);
  }

  private static int ChargeTicks(SimHarness harness, SkillAsset skill) {
    return Ticks(harness, skill.ChargeDurationMs);
  }
}
