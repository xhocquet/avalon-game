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

// Snailhead's Secondary: the trail lifecycle. A cast arms a TrailEmitter on the caster; TrailSystem
// drops one TrailSegment entity per interval at the caster's feet until the row's count is spent, and
// each segment slows the hostiles its width catches for the row's buff window.
//
// Dummies are hand-built the way the other Snailhead suites build them - a real minion is steered
// mid-test - and stood right on the drop point, which is the caster's spawn position when it holds still.
public class SnailTrailTests {
  private const int CasterPlayerId = 1;
  private const int CasterTeamId = 1;
  private const int EnemyTeamId = 2;
  private const int Secondary = (int)SkillSlot.Secondary;

  // The whole trail is authored in one row: it is self-cast, the count and cadence and width and
  // per-segment lifetime all trace back to it, and so does the slow. A hardcoded 8 or 400ms fails here.
  [Fact]
  public void EveryTrailNumber_TracesBackToTheAssetRow() {
    var harness = CreateSnailheadHarness();
    var skill = TrailAsset(harness);

    skill.IsSelfCast.Should().BeTrue();
    skill.HasTrail.Should().BeTrue();
    skill.TrailSegmentCount.Should().BePositive();
    skill.TrailSegmentIntervalMs.Should().BePositive();
    skill.TrailWidth.Should().BeGreaterThan(FP64.Zero);
    skill.TrailDurationMsAtRank(2).Should().Be(skill.TrailDurationMs + skill.TrailDurationMsPerRank);
    skill.BuffSpecs.Should().ContainSingle(s => s.Stat == StatType.MoveSpeed);
    skill.BuffSpecs[0].MagnitudeAtRank(1).Should().BeLessThan(FP64.Zero, "the slime is a slow, not a buff");

    Learn(harness);
    var dropTick = Cast(harness);

    // First circle is under the caster's feet on the cast tick; the emitter still owes count - 1.
    var segment = Segments(harness).Should().ContainSingle().Subject;
    segment.SkillAssetId.Should().Be(AssetIds.SkillSnailheadSecondary);
    segment.Rank.Should().Be(1);
    segment.TeamId.Should().Be(CasterTeamId);
    segment.Width.Should().Be(skill.TrailWidth);
    segment.SourceUnitId.Should().Be(UnitId(harness, Caster(harness)));
    segment.ExpiryTick.Should().Be(dropTick + Ticks(harness, skill.TrailDurationMsAtRank(1)));

    var emitter = Emitter(harness);
    emitter.SkillAssetId.Should().Be(AssetIds.SkillSnailheadSecondary);
    emitter.Rank.Should().Be(1);
    emitter.IntervalTicks.Should().Be(Ticks(harness, skill.TrailSegmentIntervalMs));
    emitter.SegmentLifetimeTicks.Should().Be(Ticks(harness, skill.TrailDurationMsAtRank(1)));
    emitter.SegmentsRemaining.Should().Be(skill.TrailSegmentCount - 1);
  }

  [Fact]
  public void Cast_DropsOneSegmentPerIntervalUpToTheRowsCount() {
    var harness = CreateSnailheadHarness();
    var skill = TrailAsset(harness);

    Learn(harness);
    var dropTick = Cast(harness);
    var interval = Ticks(harness, skill.TrailSegmentIntervalMs);

    Segments(harness).Count.Should().Be(1);

    for (var drop = 2; drop <= skill.TrailSegmentCount; drop++) {
      AdvanceTo(harness, dropTick + interval * (drop - 1) - 1);
      Segments(harness).Count.Should().Be(drop - 1, "nothing new between intervals");

      AdvanceTo(harness, dropTick + interval * (drop - 1));
      Segments(harness).Count.Should().Be(drop);
    }

    // Count spent: the emitter is reaped and never drops one more.
    harness.Frame.Has<TrailEmitter>(Caster(harness)).Should().BeFalse();
    AdvanceTo(harness, dropTick + interval * (skill.TrailSegmentCount + 1));
    Segments(harness).Count.Should().Be(skill.TrailSegmentCount);
  }

  [Fact]
  public void SegmentsAreLaidBehindTheMovingCaster() {
    var harness = CreateSnailheadHarness();
    var skill = TrailAsset(harness);

    Learn(harness);
    var origin = CasterPos(harness);
    var castTick = Cast(harness, SimHarness.MoveCommand(CasterPlayerId, 0, origin.x + FP64.FromInt(6), origin.z));
    AdvanceTo(harness, castTick + Ticks(harness, skill.TrailSegmentIntervalMs) * 3);

    var laid = SegmentsByDropOrder(harness);
    laid.Should().HaveCountGreaterThan(2);
    laid.Select(s => s.pos.x).Should().BeInAscendingOrder("each circle drops where the caster then stood");
    (laid.Last().pos.x - laid.First().pos.x).Should().BeGreaterThan(skill.TrailWidth);
  }

  [Fact]
  public void AHostileTouchingASegment_IsSlowedByTheRowsPercentThenItWearsOff() {
    var harness = CreateSnailheadHarness();
    var skill = TrailAsset(harness);

    Learn(harness);
    var origin = CasterPos(harness);
    var enemy = SpawnDummy(harness, origin, EnemyTeamId, moveSpeed: 5);
    var speedBefore = StatOf(harness, enemy, StatType.MoveSpeed);

    Cast(harness);
    harness.Tick(); // the segment born on the cast tick contact-tests the tick after

    var expected = speedBefore + speedBefore * skill.BuffSpecs[0].MagnitudeAtRank(1);
    StatOf(harness, enemy, StatType.MoveSpeed).Should().Be(expected);

    var entry = Entries(harness, enemy).Should().ContainSingle().Subject;
    entry.SourceId.Should().Be(AssetIds.SkillSnailheadSecondary);
    entry.Stat.Should().Be(StatType.MoveSpeed);

    // Walk it clear and let the last refresh lapse: the slow wears off a buff-window later.
    var leftAt = entry.ExpiryTick - Ticks(harness, skill.BuffDurationMs);
    MoveOff(harness, enemy, origin);
    AdvanceTo(harness, leftAt + Ticks(harness, skill.BuffDurationMs));
    StatOf(harness, enemy, StatType.MoveSpeed).Should().Be(speedBefore);
    Entries(harness, enemy).Should().BeEmpty();
  }

  [Fact]
  public void StandingInTheTrail_KeepsTheSlowRefreshedPastOneBuffWindow() {
    var harness = CreateSnailheadHarness();
    var skill = TrailAsset(harness);

    Learn(harness);
    var origin = CasterPos(harness);
    var enemy = SpawnDummy(harness, origin, EnemyTeamId, moveSpeed: 5);
    var speedBefore = StatOf(harness, enemy, StatType.MoveSpeed);

    var castTick = Cast(harness);

    // Well past a single buff window, but the enemy never leaves the slime.
    AdvanceTo(harness, castTick + Ticks(harness, skill.BuffDurationMs) * 2);
    StatOf(harness, enemy, StatType.MoveSpeed).Should().BeLessThan(speedBefore);
    Entries(harness, enemy).Should().ContainSingle(e => e.SourceId == AssetIds.SkillSnailheadSecondary);
  }

  [Fact]
  public void OnlyHostilesAreCaught_FriendlyAndStructureUntouched() {
    var harness = CreateSnailheadHarness();

    Learn(harness);
    var origin = CasterPos(harness);
    var friendly = SpawnDummy(harness, origin, CasterTeamId, moveSpeed: 5);
    var structure = SpawnStructure(harness, origin, EnemyTeamId);
    var friendlySpeed = StatOf(harness, friendly, StatType.MoveSpeed);
    var structureSpeed = StatOf(harness, structure, StatType.MoveSpeed);

    Cast(harness);
    for (var i = 0; i < 4; i++)
      harness.Tick();

    StatOf(harness, friendly, StatType.MoveSpeed).Should().Be(friendlySpeed);
    StatOf(harness, structure, StatType.MoveSpeed).Should().Be(structureSpeed);
    HasBuffs(harness, friendly).Should().BeFalse();
    HasBuffs(harness, structure).Should().BeFalse();
  }

  [Fact]
  public void ASegmentExpiresATrailDurationAfterItWasDropped() {
    var harness = CreateSnailheadHarness();
    var skill = TrailAsset(harness);

    Learn(harness);
    var dropTick = Cast(harness);
    var first = Segments(harness).Single().SegmentId;
    var lifetime = Ticks(harness, skill.TrailDurationMsAtRank(1));

    AdvanceTo(harness, dropTick + lifetime - 1);
    Segments(harness).Should().Contain(s => s.SegmentId == first);

    AdvanceTo(harness, dropTick + lifetime);
    Segments(harness).Should().NotContain(s => s.SegmentId == first);
  }

  [Fact]
  public void EachDrop_RaisesASpawnEventCarryingADistinctId() {
    var harness = CreateSnailheadHarness();
    var skill = TrailAsset(harness);

    var collector = CollectEvents(harness);
    Learn(harness);
    var dropTick = Cast(harness);
    AdvanceTo(harness, dropTick + Ticks(harness, skill.TrailSegmentIntervalMs) * skill.TrailSegmentCount);

    var spawns = collector.Collected.OfType<SkillTrailSegmentSpawnedEvent>().ToList();
    spawns.Should().HaveCount(skill.TrailSegmentCount);
    spawns.Select(s => s.SegmentId).Should().OnlyHaveUniqueItems();
    spawns.Should().OnlyContain(s => s.SkillAssetId == AssetIds.SkillSnailheadSecondary);
    spawns.Should().OnlyContain(s => s.LifetimeTicks == Ticks(harness, skill.TrailDurationMsAtRank(1)));
    spawns.Should().OnlyContain(s => s.Width == skill.TrailWidth);
  }

  [Fact]
  public void TheCasterDying_StopsTheTrailButLeavesTheSegmentsAlreadyDown() {
    var harness = CreateSnailheadHarness();
    var skill = TrailAsset(harness);
    var interval = Ticks(harness, skill.TrailSegmentIntervalMs);

    Learn(harness);
    var dropTick = Cast(harness);
    AdvanceTo(harness, dropTick + interval);
    var downWhenKilled = Segments(harness).Count;
    downWhenKilled.Should().BeGreaterThan(0);

    harness.Frame.Get<Health>(Caster(harness)).Current = FP64.Zero;
    harness.Tick(); // RespawnSystem picks the death up

    harness.Frame.Has<TrailEmitter>(Caster(harness)).Should().BeFalse();

    for (var i = 0; i < 4 * interval; i++)
      harness.Tick();

    Segments(harness).Count.Should().BeLessOrEqualTo(downWhenKilled, "no new circles after the caster died");
  }

  // --- setup helpers ---

  private static SimHarness CreateSnailheadHarness() {
    var harness = SimHarness.CreateInitialized(spawnHeroesNow: false);
    harness.AssetRegistry.Get<WaveRulesAsset>().MinionsPerWave = 0;

    harness.Tick(
      SimHarness.SelectFactionCommand(1, 0, AssetIds.FactionSnailheads),
      SimHarness.SelectFactionCommand(2, 0, AssetIds.FactionSnailheads));
    DisableAutoAttacks(harness);
    return harness;
  }

  private static void DisableAutoAttacks(SimHarness harness) {
    var frame = harness.Frame;
    var attackers = new List<EntityRef>();
    var filter = frame.Filter<Combat>();
    while (filter.Next(out var entity))
      attackers.Add(entity);

    foreach (var entity in attackers)
      frame.Remove<Combat>(entity);
  }

  private static void Learn(SimHarness harness) {
    harness.Tick(SimHarness.UpgradeSkillCommand(CasterPlayerId, 0, Secondary));
    DisableAutoAttacks(harness);
  }

  // Casts and returns the tick the cast (and so the first drop) lands on, which every clock is measured
  // from. Read the caster position before this if a test needs where the first circle drops.
  private static int Cast(SimHarness harness, params ICommand[] extra) {
    var castTick = harness.Frame.Tick;
    var commands = new List<ICommand> { SimHarness.CastSkillCommand(CasterPlayerId, 0, Secondary) };
    commands.AddRange(extra);
    harness.Tick(commands.ToArray());
    DisableAutoAttacks(harness);
    return castTick;
  }

  private static void AdvanceTo(SimHarness harness, int tick) {
    while (harness.Frame.Tick <= tick) {
      harness.Tick();
      DisableAutoAttacks(harness);
    }
  }

  private static void MoveOff(SimHarness harness, EntityRef enemy, FPVector3 origin) {
    harness.Frame.Get<TransformComponent>(enemy).Position =
      origin + new FPVector3(FP64.FromInt(20), FP64.Zero, FP64.Zero);
  }

  private static EntityRef SpawnDummy(SimHarness harness, FPVector3 position, int teamId, int moveSpeed) {
    var frame = harness.Frame;
    var entity = frame.CreateEntity();

    frame.Add(entity, TransformFactory.At(position));
    frame.Add(entity, new UnitIdentity {
      UnitId = UnitLookup.NextUnitId(ref frame),
      UnitTypeId = SimulationSetup.MinionUnitTypeId
    });
    frame.Add(entity, new Team { TeamId = teamId });
    frame.Add(entity, new Minion { WaveId = 99 });
    frame.Add(entity, new Health(FP64.FromInt(500)));
    frame.Add(entity, Stats.Create().With(StatType.MoveSpeed, FP64.FromInt(moveSpeed)));

    return entity;
  }

  private static EntityRef SpawnStructure(SimHarness harness, FPVector3 position, int teamId) {
    var frame = harness.Frame;
    var entity = frame.CreateEntity();

    frame.Add(entity, TransformFactory.At(position));
    frame.Add(entity, new UnitIdentity {
      UnitId = UnitLookup.NextUnitId(ref frame),
      UnitTypeId = SimulationSetup.TurretUnitTypeId
    });
    frame.Add(entity, new Team { TeamId = teamId });
    frame.Add(entity, new Turret { TurretId = 99 });
    frame.Add(entity, new Health(FP64.FromInt(1500)));
    frame.Add(entity, Stats.Create().With(StatType.MoveSpeed, FP64.FromInt(5)));

    return entity;
  }

  private static EventCollector CollectEvents(SimHarness harness) {
    var collector = new EventCollector();
    collector.BeginTick(harness.Frame.Tick);
    harness.Frame.EventRaiser = collector;
    return collector;
  }

  // --- readers ---

  private static SkillAsset TrailAsset(SimHarness harness) {
    return harness.AssetRegistry.Get<SkillAsset>(AssetIds.SkillSnailheadSecondary);
  }

  private static EntityRef Caster(SimHarness harness) {
    return harness.FindHero(CasterPlayerId);
  }

  private static FPVector3 CasterPos(SimHarness harness) {
    return harness.Frame.GetReadOnly<TransformComponent>(Caster(harness)).Position;
  }

  private static TrailEmitter Emitter(SimHarness harness) {
    return harness.Frame.GetReadOnly<TrailEmitter>(Caster(harness));
  }

  private static List<TrailSegment> Segments(SimHarness harness) {
    var list = new List<TrailSegment>();
    var frame = harness.Frame;
    var filter = frame.Filter<TrailSegment>();
    while (filter.Next(out var entity))
      list.Add(frame.GetReadOnly<TrailSegment>(entity));

    return list;
  }

  private static List<(int id, FPVector3 pos)> SegmentsByDropOrder(SimHarness harness) {
    var list = new List<(int id, FPVector3 pos)>();
    var frame = harness.Frame;
    var filter = frame.Filter<TrailSegment, TransformComponent>();
    while (filter.Next(out var entity))
      list.Add((frame.GetReadOnly<TrailSegment>(entity).SegmentId,
        frame.GetReadOnly<TransformComponent>(entity).Position));

    return list.OrderBy(s => s.id).ToList();
  }

  private static FP64 StatOf(SimHarness harness, EntityRef entity, StatType stat) {
    return harness.Frame.GetReadOnly<Stats>(entity).Get(stat);
  }

  private static bool HasBuffs(SimHarness harness, EntityRef entity) {
    var frame = harness.Frame;
    return frame.Has<StatBuffs>(entity) && StatBuffApplication.ActiveCount(ref frame, entity) > 0;
  }

  private static int UnitId(SimHarness harness, EntityRef entity) {
    return harness.Frame.GetReadOnly<UnitIdentity>(entity).UnitId;
  }

  private static int Ticks(SimHarness harness, int milliseconds) {
    var frame = harness.Frame;
    return TickMath.MsToTicksCeil(ref frame, milliseconds);
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
