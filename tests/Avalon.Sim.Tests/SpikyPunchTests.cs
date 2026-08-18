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

// Crystal Giant's Primary, and with it the empowered-attack lifecycle: a cast that does nothing on
// its own, waits out a duration, and is spent by the next auto-attack that lands.
//
// The dummy is hand-built rather than spawned through MinionFactory so it holds still and cannot
// fight back, and AttackOnce strips Combat off everything but the caster for the tick it drives, so
// the health delta it returns is one attack from one attacker and nothing else.
public class SpikyPunchTests {
  private const int CasterPlayerId = 1;
  private const int EnemyTeamId = 2;
  private const int Primary = (int)SkillSlot.Primary;

  [Fact]
  public void Cast_ArmsTheNextAttackAndDealsNoDamageByItself() {
    var harness = CreateCrystalGiantHarness();
    var enemy = SpawnDummy(harness);
    var healthBefore = harness.Frame.GetReadOnly<Health>(enemy).Current;

    LearnAndCast(harness);

    IsArmed(harness).Should().BeTrue();
    harness.Frame.GetReadOnly<Health>(enemy).Current.Should().Be(healthBefore, "the cast hits nothing");
  }

  [Fact]
  public void TheNextAttack_LandsAtTheRowsMultiplierAndSpendsTheCharge() {
    var harness = CreateCrystalGiantHarness();
    var skill = SpikyPunchAsset(harness);
    var enemy = SpawnDummy(harness);

    LearnAndCast(harness);
    var dealt = AttackOnce(harness, enemy);

    dealt.Should().Be(ExpectedHit(harness, enemy, skill.ProcDamageMultiplierAtRank(1)));
    IsArmed(harness).Should().BeFalse();
  }

  [Fact]
  public void TheAttackAfterThat_IsAPlainHitAgain() {
    var harness = CreateCrystalGiantHarness();
    var enemy = SpawnDummy(harness);

    LearnAndCast(harness);
    var empowered = AttackOnce(harness, enemy);
    var plain = AttackOnce(harness, enemy);

    plain.Should().Be(ExpectedHit(harness, enemy, FP64.One));
    empowered.Should().Be(plain * SpikyPunchAsset(harness).ProcDamageMultiplierAtRank(1));
  }

  // The whole proc is authored in the one row. A hardcoded 4x or 5s fails here rather than shipping.
  [Fact]
  public void EveryProcNumber_TracesBackToTheAssetRow() {
    var harness = CreateCrystalGiantHarness();
    var skill = SpikyPunchAsset(harness);

    skill.ProcDurationMs.Should().BePositive();
    skill.ProcDamageMultiplier.Should().BeGreaterThan(FP64.One, "a multiplier of 1 empowers nothing");
    skill.ProcDamageMultiplierAtRank(2)
      .Should().Be(skill.ProcDamageMultiplier + skill.ProcDamageMultiplierPerRank);
    skill.ProcResetsAttackCooldown.Should().Be(1, "Spiky Punch is authored to reset the swing timer");

    LearnAndCast(harness);

    ref readonly var proc =
      ref harness.Frame.GetReadOnly<AttackProcComponent>(harness.FindHero(CasterPlayerId));
    proc.SourceId.Should().Be(AssetIds.SkillCrystalGiantPrimary);
    proc.DamageMultiplier.Should().Be(skill.ProcDamageMultiplierAtRank(1));
  }

  [Fact]
  public void AnUnusedCharge_LapsesAfterTheAuthoredDuration() {
    var harness = CreateCrystalGiantHarness();
    var enemy = SpawnDummy(harness);

    var castTick = LearnAndCast(harness);
    var durationTicks = DurationTicks(harness, SpikyPunchAsset(harness).ProcDurationMs);

    AdvanceTo(harness, castTick + durationTicks - 1);
    IsArmed(harness).Should().BeTrue("the charge holds through the last tick of its duration");

    AdvanceTo(harness, castTick + durationTicks);
    IsArmed(harness).Should().BeFalse();

    AttackOnce(harness, enemy).Should().Be(ExpectedHit(harness, enemy, FP64.One));
  }

  // Arming resets the swing timer, so a punch cast right after an auto-attack goes out on the cast
  // tick instead of waiting out the rest of that auto's cooldown.
  [Fact]
  public void ArmingResetsTheSwingTimerSoThePunchLandsAtOnce() {
    var harness = CreateCrystalGiantHarness();
    var skill = SpikyPunchAsset(harness);
    var enemy = SpawnDummy(harness);
    harness.Tick(SimHarness.UpgradeSkillCommand(CasterPlayerId, 0, Primary));

    var plain = AttackOnce(harness, enemy);
    var cooldownAfterAuto = AttackCooldownRemaining(harness);
    cooldownAfterAuto.Should().BeGreaterThan(1, "the auto that just landed has to leave a real wait");

    var empowered = TickAgainst(harness, enemy, SimHarness.CastSkillCommand(CasterPlayerId, 0, Primary));

    empowered.Should().Be(plain * skill.ProcDamageMultiplierAtRank(1),
      "the punch should land on the cast tick rather than waiting out the swing timer");
    AttackCooldownRemaining(harness).Should().BeGreaterThan(0,
      "the cooldown restarts from the punch - the reset buys one swing, not a faster attack rate");
  }

  // The reset is authored, not assumed: a proc that should not skip the wait leaves the field 0.
  [Fact]
  public void WithoutTheAuthoredReset_TheSwingTimerIsLeftAlone() {
    var harness = CreateCrystalGiantHarness();
    var enemy = SpawnDummy(harness);
    var hero = harness.FindHero(CasterPlayerId);

    AttackOnce(harness, enemy);
    var cooldownBefore = AttackCooldownRemaining(harness);

    var frame = harness.Frame;
    AttackProcs.Arm(ref frame, hero, AssetIds.SkillCrystalGiantPrimary, FP64.FromInt(4),
      durationTicks: 60);

    AttackCooldownRemaining(harness).Should().Be(cooldownBefore);
  }

  [Fact]
  public void Death_DropsTheChargeRatherThanSavingItForTheRespawn() {
    var harness = CreateCrystalGiantHarness();

    LearnAndCast(harness);
    IsArmed(harness).Should().BeTrue();

    harness.Frame.Get<Health>(harness.FindHero(CasterPlayerId)).Current = FP64.Zero;
    harness.Tick(); // RespawnSystem picks the death up

    IsArmed(harness).Should().BeFalse();
  }

  // The multiplier lands before mitigation, the same place a crit's does, so the hit is worth 4x a
  // plain one against any armor value rather than 4x the post-mitigation number.
  [Fact]
  public void TheMultiplierLandsBeforeMitigation() {
    var harness = CreateCrystalGiantHarness();
    var enemy = SpawnDummy(harness);
    harness.Frame.Add(enemy, StatsComponent.Create().With(StatType.Armor, FP64.FromInt(100)));
    harness.Tick(SimHarness.UpgradeSkillCommand(CasterPlayerId, 0, Primary));

    // The cast resets the swing timer, so the punch is the hit that lands on the cast tick.
    var plain = AttackOnce(harness, enemy);
    var empowered = TickAgainst(harness, enemy, SimHarness.CastSkillCommand(CasterPlayerId, 0, Primary));

    // 100 armor halves an incoming hit, so a post-mitigation multiply would land 4x a halved number
    // instead of half a quadrupled one. They agree here only because the order is right.
    plain.Should().Be(ExpectedHit(harness, enemy, FP64.One));
    empowered.Should().Be(plain * SpikyPunchAsset(harness).ProcDamageMultiplierAtRank(1));
  }

  // The proc describes itself in its own event rather than as a flag on the hit, and points back at
  // the hit it modified. An attack that spent several effects raises one of these per effect, which
  // is the whole reason the skill id does not live on AttackHitEvent.
  [Fact]
  public void TheConsumedProcRaisesItsOwnEventPointingAtTheHit() {
    var harness = CreateCrystalGiantHarness();
    var enemy = SpawnDummy(harness);
    var skill = SpikyPunchAsset(harness);

    LearnAndCast(harness);
    var collector = new EventCollector();
    collector.BeginTick(harness.Frame.Tick);
    harness.Frame.EventRaiser = collector;

    AttackOnce(harness, enemy);
    AttackOnce(harness, enemy);

    var hits = collector.Collected.OfType<AttackHitEvent>().ToList();
    hits.Should().HaveCount(2);
    hits.Select(h => h.AttackHitId).Should().OnlyHaveUniqueItems("each hit is its own thing to point at");

    var procs = collector.Collected.OfType<AttackProcConsumedEvent>().ToList();
    procs.Should().ContainSingle("the charge was spent on the first hit");
    procs[0].AttackHitId.Should().Be(hits[0].AttackHitId);
    procs[0].SkillAssetId.Should().Be(AssetIds.SkillCrystalGiantPrimary);
    procs[0].DamageMultiplier.Should().Be(skill.ProcDamageMultiplierAtRank(1));

    var hero = harness.FindHero(CasterPlayerId);
    procs[0].AttackerUnitId.Should().Be(harness.Frame.GetReadOnly<UnitIdComponent>(hero).UnitId);
    procs[0].TargetUnitId.Should().Be(harness.Frame.GetReadOnly<UnitIdComponent>(enemy).UnitId);
  }

  // Ids come off a frame-state counter, so they have to be allocated whether or not anything is
  // listening - a peer that raises no events must still burn the same ids or the frames stop hashing
  // the same.
  [Fact]
  public void HitIdsAreAllocatedWithNoEventRaiserAttached() {
    var harness = CreateCrystalGiantHarness();
    var enemy = SpawnDummy(harness);
    harness.Frame.EventRaiser = null;

    var before = NextHitId(harness);
    AttackOnce(harness, enemy);
    NextHitId(harness).Should().Be(before + 1);
  }

  // --- setup helpers ---

  // The harness defaults every player to Hairy Wizards, whose skill set is empty, so go through the
  // real faction-select path to get a Crystal Giant on the board.
  private static SimHarness CreateCrystalGiantHarness() {
    var harness = SimHarness.CreateInitialized(spawnHeroesNow: false);
    harness.Tick(
      SimHarness.SelectFactionCommand(1, 0, AssetIds.FactionCrystalWarriors),
      SimHarness.SelectFactionCommand(2, 0, AssetIds.FactionCrystalWarriors));

    return harness;
  }

  // Returns the tick the cast executed on, which is what the expiry is measured from.
  private static int LearnAndCast(SimHarness harness) {
    harness.Tick(SimHarness.UpgradeSkillCommand(CasterPlayerId, 0, Primary));

    var castTick = harness.Frame.Tick;
    harness.Tick(SimHarness.CastSkillCommand(CasterPlayerId, 0, Primary));
    return castTick;
  }

  // Drives exactly one auto-attack through the real intent path - AttackIntentSystem resolves the
  // order, DamageSystem lands the hit on the same tick - by clearing the swing timer first.
  private static FP64 AttackOnce(SimHarness harness, EntityRef target) {
    var frame = harness.Frame;
    frame.Get<Combat>(harness.FindHero(CasterPlayerId)).CooldownRemainingTicks = 0;
    return TickAgainst(harness, target);
  }

  // One tick with the caster holding an attack order on the target, returning what came off it. The
  // swing timer is left alone, so whether an attack lands is the sim's call - which is what lets a
  // cast that resets the cooldown show up as damage. The dummy is parked inside the hero's reach
  // first; it carries no nav agent, so it stays put.
  private static FP64 TickAgainst(SimHarness harness, EntityRef target, params ICommand[] commands) {
    var frame = harness.Frame;
    var hero = harness.FindHero(CasterPlayerId);
    DisableOtherAttackers(harness, hero);

    var heroPosition = frame.GetReadOnly<TransformComponent>(hero).Position;
    frame.Get<TransformComponent>(target).Position = heroPosition + FPVector3.Right;
    UnitIntent.SetAttackTarget(ref frame, hero, frame.GetReadOnly<UnitIdComponent>(target).UnitId);

    var healthBefore = frame.GetReadOnly<Health>(target).Current;
    harness.Tick(commands);
    return healthBefore - harness.Frame.GetReadOnly<Health>(target).Current;
  }

  private static int AttackCooldownRemaining(SimHarness harness) {
    return harness.Frame.GetReadOnly<Combat>(harness.FindHero(CasterPlayerId)).CooldownRemainingTicks;
  }

  // Leaves the caster as the only thing on the board that can deal damage, so the health delta of
  // the tick it drives is one attack. Re-run per attack because waves keep spawning new attackers.
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

  // What one attack of this hero should take off that target at the given multiplier. Crit is
  // authored at 0 on every hero row, so attack damage and mitigation are the only variables.
  private static FP64 ExpectedHit(SimHarness harness, EntityRef target, FP64 multiplier) {
    var frame = harness.Frame;
    ref readonly var stats = ref frame.GetReadOnly<StatsComponent>(harness.FindHero(CasterPlayerId));
    stats.CritChance.Should().Be(FP64.Zero, "these expectations assume no crit roll can land");

    return DamageApplication.Mitigate(ref frame, target, stats.AttackDamage * multiplier);
  }

  // The counter is created on the first allocation, so before any hit there is no singleton to read.
  private static int NextHitId(SimHarness harness) {
    var frame = harness.Frame;
    return frame.TryGetSingleton<AttackHitIdCounter>(out _)
      ? frame.GetSingleton<AttackHitIdCounter>().NextId
      : IdCounter<AttackHitIdCounter>.FirstId;
  }

  private static bool IsArmed(SimHarness harness) {
    var frame = harness.Frame;
    return AttackProcs.IsArmed(ref frame, harness.FindHero(CasterPlayerId));
  }

  private static void AdvanceTo(SimHarness harness, int tick) {
    while (harness.Frame.Tick <= tick)
      harness.Tick();
  }

  private static SkillAsset SpikyPunchAsset(SimHarness harness) {
    return harness.AssetRegistry.Get<SkillAsset>(AssetIds.SkillCrystalGiantPrimary);
  }

  private static int DurationTicks(SimHarness harness, int milliseconds) {
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
