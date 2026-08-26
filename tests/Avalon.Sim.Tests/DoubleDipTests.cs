using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using Meesles.Avalon.Sim.Factories;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using Xunit;

namespace Meesles.Avalon.Sim.Tests;

// Pickle Knight's Secondary, and with it the attack-burst lifecycle: a cast that deals no damage of
// its own, clears the swing timer, and buys a run of auto-attacks spaced at the row's delay instead
// of the caster's attack period.
//
// Built the same way as SpikyPunchTests - a hand-made dummy that holds still and cannot fight back,
// and every other Combat stripped for the ticks these drive, so a health delta is one attack from
// one attacker and nothing else.
public class DoubleDipTests {
  private const int CasterPlayerId = 1;
  private const int EnemyTeamId = 2;
  private const int Secondary = (int)SkillSlot.Secondary;

  [Fact]
  public void Cast_QueuesTheRowsExtraSwingsAndHitsNothingByItself() {
    var harness = CreatePickleKnightHarness();
    var enemy = SpawnDummy(harness); // Parked at the origin, out of the hero's reach
    var healthBefore = harness.Frame.GetReadOnly<Health>(enemy).Current;

    LearnAndCast(harness);

    Remaining(harness).Should().Be(DoubleDipAsset(harness).BurstAttackCountAtRank(1) - 1);
    harness.Frame.GetReadOnly<Health>(enemy).Current.Should().Be(healthBefore, "the cast hits nothing");
  }

  // The whole point: two swings back to back at the row's spacing, the first on the cast tick.
  [Fact]
  public void TheBurst_LandsItsAttacksAtTheRowsSpacing() {
    var harness = CreatePickleKnightHarness();
    var skill = DoubleDipAsset(harness);
    var enemy = SpawnDummy(harness);
    harness.Tick(SimHarness.UpgradeSkillCommand(CasterPlayerId, 0, Secondary));

    var plain = AttackOnce(harness, enemy);
    AttackCooldownRemaining(harness).Should()
      .BeGreaterThan(1, "the auto that just landed has to leave a real wait");

    var delayTicks = Ticks(harness, skill.BurstAttackDelayMs);
    var expected = ExpectedHitTicks(harness, delayTicks, skill.BurstAttackCountAtRank(1));
    var perTick = DamagePerTick(harness, enemy, expected[^1] + 2,
      SimHarness.CastSkillCommand(CasterPlayerId, 0, Secondary));

    HitTicks(perTick).Should().Equal(expected,
      "the cast tick swings and the queued swing follows one burst delay later");
    perTick[expected[0]].Should().Be(plain, "a burst swing is a plain attack");
    perTick[expected[1]].Should().Be(plain);
  }

  // Rank buys swings: one more per rank, so the maxed skill is five hits rather than two.
  [Fact]
  public void EachRank_AddsASwingToTheBurst() {
    var harness = CreatePickleKnightHarness();
    var skill = DoubleDipAsset(harness);
    var enemy = SpawnDummy(harness);
    MaxOut(harness);

    skill.BurstAttackCountAtRank(skill.MaxRank).Should().Be(skill.BurstAttackCount + skill.MaxRank - 1);

    var delayTicks = Ticks(harness, skill.BurstAttackDelayMs);
    var swings = skill.BurstAttackCountAtRank(skill.MaxRank);
    var expected = ExpectedHitTicks(harness, delayTicks, swings);
    var perTick = DamagePerTick(harness, enemy, expected[^1] + 2,
      SimHarness.CastSkillCommand(CasterPlayerId, 0, Secondary));

    HitTicks(perTick).Should().Equal(expected,
      "every swing the rank paid for lands one burst delay after the one before it");
  }

  // The burst buys its swings and stops - the attack rate afterwards is the caster's own again.
  [Fact]
  public void AfterTheLastBurstSwing_TheSwingTimerGoesBackToTheAttackPeriod() {
    var harness = CreatePickleKnightHarness();
    var enemy = SpawnDummy(harness);
    harness.Tick(SimHarness.UpgradeSkillCommand(CasterPlayerId, 0, Secondary));

    var delayTicks = Ticks(harness, DoubleDipAsset(harness).BurstAttackDelayMs);
    DamagePerTick(harness, enemy, delayTicks + 1,
      SimHarness.CastSkillCommand(CasterPlayerId, 0, Secondary));

    Remaining(harness).Should().Be(0);
    var frame = harness.Frame;
    AttackCooldownRemaining(harness)
      .Should().Be(CombatTiming.CooldownTicks(ref frame, harness.FindHero(CasterPlayerId)));
  }

  // The whole burst is authored in the one row. A hardcoded 2 swings or 150ms fails here rather than
  // shipping.
  [Fact]
  public void EveryBurstNumber_TracesBackToTheAssetRow() {
    var harness = CreatePickleKnightHarness();
    var skill = DoubleDipAsset(harness);

    skill.BurstAttackCount.Should().BeGreaterThan(1, "a burst of one swing is not a burst");
    skill.BurstAttackDelayMs.Should().BePositive();
    skill.BurstDurationMs.Should().BePositive();
    skill.BurstResetsAttackCooldown.Should().Be(1, "Double Dip is authored to reset the swing timer");
    skill.BurstAttackCountAtRank(2).Should().Be(skill.BurstAttackCount + skill.BurstAttackCountPerRank);

    LearnAndCast(harness);

    ref readonly var burst =
      ref harness.Frame.GetReadOnly<AttackBurstComponent>(harness.FindHero(CasterPlayerId));
    burst.SourceId.Should().Be(AssetIds.SkillPickleKnightSecondary);
    burst.Remaining.Should().Be(skill.BurstAttackCountAtRank(1) - 1);
    burst.DelayTicks.Should().Be(Ticks(harness, skill.BurstAttackDelayMs));
  }

  [Fact]
  public void UnspentSwings_LapseAfterTheAuthoredDuration() {
    var harness = CreatePickleKnightHarness();
    var castTick = LearnAndCast(harness);
    var durationTicks = Ticks(harness, DoubleDipAsset(harness).BurstDurationMs);

    AdvanceTo(harness, castTick + durationTicks - 1);
    Remaining(harness).Should().BePositive("the queue holds through the last tick of its duration");

    AdvanceTo(harness, castTick + durationTicks);
    Remaining(harness).Should().Be(0);
  }

  [Fact]
  public void Death_DropsTheQueuedSwingsRatherThanSavingThemForTheRespawn() {
    var harness = CreatePickleKnightHarness();

    LearnAndCast(harness);
    Remaining(harness).Should().BePositive();

    harness.Frame.Get<Health>(harness.FindHero(CasterPlayerId)).Current = FP64.Zero;
    harness.Tick(); // RespawnSystem picks the death up

    Remaining(harness).Should().Be(0);
  }

  // The spacing shortens the wait, it never lengthens it: a slow-authored burst on a fast attacker
  // would otherwise be a penalty for casting.
  [Fact]
  public void ASpacingLongerThanTheAttackPeriod_LeavesTheAttackPeriodAlone() {
    var harness = CreatePickleKnightHarness();
    var frame = harness.Frame;
    var hero = harness.FindHero(CasterPlayerId);

    AttackBursts.Queue(ref frame, hero, AssetIds.SkillPickleKnightSecondary, totalAttacks: 2,
      delayTicks: 999, durationTicks: 60);

    AttackBursts.NextCooldownTicks(ref frame, hero, defaultTicks: 40).Should().Be(40);
  }

  // The reset is authored, not assumed: a burst that should not skip the wait leaves the field 0.
  [Fact]
  public void WithoutTheAuthoredReset_TheSwingTimerIsLeftAlone() {
    var harness = CreatePickleKnightHarness();
    var enemy = SpawnDummy(harness);
    var hero = harness.FindHero(CasterPlayerId);

    AttackOnce(harness, enemy);
    var cooldownBefore = AttackCooldownRemaining(harness);

    var frame = harness.Frame;
    AttackBursts.Queue(ref frame, hero, AssetIds.SkillPickleKnightSecondary, totalAttacks: 2,
      delayTicks: 5, durationTicks: 60);

    AttackCooldownRemaining(harness).Should().Be(cooldownBefore);
  }

  // --- setup helpers ---

  // The harness defaults every player to Hairy Wizards, so go through the real faction-select path to
  // get a Pickle Knight on the board.
  private static SimHarness CreatePickleKnightHarness() {
    var harness = SimHarness.CreateInitialized(spawnHeroesNow: false);
    harness.Tick(
      SimHarness.SelectFactionCommand(1, 0, AssetIds.FactionPickleKnights),
      SimHarness.SelectFactionCommand(2, 0, AssetIds.FactionPickleKnights));

    return harness;
  }

  // Returns the tick the cast executed on, which is what the expiry is measured from.
  private static int LearnAndCast(SimHarness harness) {
    harness.Tick(SimHarness.UpgradeSkillCommand(CasterPlayerId, 0, Secondary));

    var castTick = harness.Frame.Tick;
    harness.Tick(SimHarness.CastSkillCommand(CasterPlayerId, 0, Secondary));
    return castTick;
  }

  // Ranks the skill to its cap. The points are handed over directly: levelling a hero far enough to
  // earn four of them is not what these are testing.
  private static void MaxOut(SimHarness harness) {
    var maxRank = DoubleDipAsset(harness).MaxRank;
    harness.Frame.Get<SkillsComponent>(harness.FindHero(CasterPlayerId)).SkillPoints = maxRank;

    for (var i = 0; i < maxRank; i++)
      harness.Tick(SimHarness.UpgradeSkillCommand(CasterPlayerId, 0, Secondary));

    harness.Frame.GetReadOnly<SkillsComponent>(harness.FindHero(CasterPlayerId))
      .GetRank(Secondary).Should().Be(maxRank);
  }

  // Drives exactly one auto-attack through the real intent path by clearing the swing timer first.
  // Runs one whole attack, swing through hit. The damage lands a wind-up after the swing, so the
  // window has to cover both and the one hit inside it is the sum.
  private static FP64 AttackOnce(SimHarness harness, EntityRef target) {
    var frame = harness.Frame;
    var hero = harness.FindHero(CasterPlayerId);
    frame.Get<Combat>(hero).CooldownRemainingTicks = 0;

    var windup = CombatTiming.WindupTicks(ref frame, hero, CombatTiming.CooldownTicks(ref frame, hero));
    return DamagePerTick(harness, target, windup + 1).Aggregate(FP64.Zero, (total, d) => total + d);
  }

  // One entry per tick of what the caster took off the target, holding an attack order on it the
  // whole way. The swing timer is left alone, so which ticks land a hit is the sim's call - which is
  // what the burst spacing shows up in.
  //
  // Read off AttackHitEvent rather than the target's health, because a wave spawned mid-tick can hit
  // the dummy before DisableOtherAttackers gets to strip it, and over a long burst that shows up as a
  // swing the caster never took.
  private static List<FP64> DamagePerTick(SimHarness harness, EntityRef target, int ticks,
    params ICommand[] firstTickCommands) {
    var damage = new List<FP64>(ticks);
    var collector = new EventCollector();

    for (var i = 0; i < ticks; i++) {
      var frame = harness.Frame;
      var hero = harness.FindHero(CasterPlayerId);
      DisableOtherAttackers(harness, hero);

      // Re-parked every tick: the dummy carries no nav agent, but the hero moves.
      var heroPosition = frame.GetReadOnly<TransformComponent>(hero).Position;
      frame.Get<TransformComponent>(target).Position = heroPosition + FPVector3.Right;

      var attackerUnitId = frame.GetReadOnly<UnitIdComponent>(hero).UnitId;
      var targetUnitId = frame.GetReadOnly<UnitIdComponent>(target).UnitId;
      UnitIntent.SetAttackTarget(ref frame, hero, targetUnitId);

      collector.BeginTick(frame.Tick);
      frame.EventRaiser = collector;
      harness.Tick(i == 0 ? firstTickCommands : []);
      damage.Add(HitDamage(collector, attackerUnitId, targetUnitId));
    }

    return damage;
  }

  private static FP64 HitDamage(EventCollector collector, int attackerUnitId, int targetUnitId) {
    return collector.Collected.OfType<AttackHitEvent>()
      .Where(hit => hit.AttackerUnitId == attackerUnitId && hit.TargetUnitId == targetUnitId)
      .Aggregate(FP64.Zero, (total, hit) => total + hit.Damage);
  }

  // Swing i goes out at i * delayTicks and lands its own wind-up later. Every swing but the last is a
  // burst swing, which pays the burst spacing as its cooldown and so has its wind-up held under that;
  // the last one pays the caster's own period and gets the full authored wind-up.
  private static List<int> ExpectedHitTicks(SimHarness harness, int delayTicks, int swings) {
    var frame = harness.Frame;
    var hero = harness.FindHero(CasterPlayerId);
    var burstWindup = CombatTiming.WindupTicks(ref frame, hero, delayTicks);
    var fullWindup = CombatTiming.WindupTicks(ref frame, hero, CombatTiming.CooldownTicks(ref frame, hero));

    return Enumerable.Range(0, swings)
      .Select(i => i * delayTicks + (i < swings - 1 ? burstWindup : fullWindup))
      .ToList();
  }

  private static List<int> HitTicks(List<FP64> damagePerTick) {
    return Enumerable.Range(0, damagePerTick.Count)
      .Where(i => damagePerTick[i] > FP64.Zero)
      .ToList();
  }

  private static int AttackCooldownRemaining(SimHarness harness) {
    return harness.Frame.GetReadOnly<Combat>(harness.FindHero(CasterPlayerId)).CooldownRemainingTicks;
  }

  // Leaves the caster as the only thing on the board that can deal damage. Re-run per tick because
  // waves keep spawning new attackers.
  private static void DisableOtherAttackers(SimHarness harness, EntityRef keep) {
    var frame = harness.Frame;
    var others = new List<EntityRef>();

    var filter = frame.Filter<Combat>();
    while (filter.Next(out var entity))
      if (entity != keep)
        others.Add(entity);

    foreach (var entity in others)
      frame.Remove<Combat>(entity);
  }

  private static int Remaining(SimHarness harness) {
    var frame = harness.Frame;
    return AttackBursts.Remaining(ref frame, harness.FindHero(CasterPlayerId));
  }

  private static void AdvanceTo(SimHarness harness, int tick) {
    while (harness.Frame.Tick <= tick)
      harness.Tick();
  }

  private static SkillAsset DoubleDipAsset(SimHarness harness) {
    return harness.AssetRegistry.Get<SkillAsset>(AssetIds.SkillPickleKnightSecondary);
  }

  private static int Ticks(SimHarness harness, int milliseconds) {
    var frame = harness.Frame;
    return TickMath.MsToTicksCeil(ref frame, milliseconds);
  }

  private static EntityRef SpawnDummy(SimHarness harness) {
    var frame = harness.Frame;
    var entity = frame.CreateEntity();

    frame.Add(entity, TransformFactory.At(FPVector3.Zero));
    frame.Add(entity, new UnitIdComponent {
      UnitId = UnitLookup.NextUnitId(ref frame),
      UnitTypeId = SimulationSetup.MinionUnitTypeId
    });
    frame.Add(entity, new TeamComponent(EnemyTeamId));
    frame.Add(entity, new Health(100000)); // Deep enough that nothing here can kill it
    frame.Add(entity, new Minion { WaveId = 0 });

    return entity;
  }
}
