using System.Collections.Generic;
using FluentAssertions;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using Xunit;
using Xunit.Abstractions;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Deterministic.Navigation;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Tests;

// Rollback coverage — see RollbackHarness for why DeterminismBaselineTests cannot reach this and
// what the server/client split means. Short version: the baseline re-runs from tick 0, so every
// per-system cache is rebuilt identically in both runs; only a rollback replays ticks over state
// that was restored, which is what exposes a cache that did not roll back with it.
public class RollbackDeterminismTests {
  private const int WarmupTicks = 300;
  private const int MispredictedTicks = 15;
  private const int ReplayTicks = 90;

  // Enough to walk the hero clear of the navmesh edge and back several times over.
  private const int DriftTicks = 40;
  private const int DriftWarmupTicks = 60;

  // Far more than the 4 ranks a slot can hold, so neither stream ever runs dry and stops diverging.
  private const int SkillPointPool = 40;

  // Well past the cap, so the discarded branch lands every level a hero can reach at once.
  private const int LevelUpExperience = 1_000_000;

  // Picked so the snapshot lands mid-volley: Crystal Bullets is off cooldown every ~188 ticks and
  // each volley stays airborne for ~48 after that. AssertProjectilesAreInFlight holds it honest.
  private const int ProjectileWarmupTicks = 230;

  private readonly ITestOutputHelper _output;

  public RollbackDeterminismTests(ITestOutputHelper output) {
    _output = output;
  }

  // Broad guard: the ordinary game — heroes traversing the map, waves on the field — across a
  // rollback boundary. Here so that the next per-system cache added outside frame state fails in
  // CI instead of as a live desync.
  [Fact]
  public void Rollback_AfterMispredictedBranch_ReplayMatchesServerHashes() {
    var rollback = RollbackHarness.Create();

    rollback.Advance(WarmupTicks, Authoritative);
    rollback.InSync.Should().BeTrue(
      "the two sims must be identical before the rollback so any later difference is " +
      "attributable to the rollback alone");

    rollback.MispredictAndRollback(MispredictedTicks, Mispredicted);
    rollback.Client.Frame.Tick.Should().Be(WarmupTicks, "the rollback should restore the snapshot tick");
    rollback.InSync.Should().BeTrue(
      "restoring the snapshot should put the client back on the server's state — if this fails, " +
      "the leak is in frame/system-snapshot restore rather than in an unsnapshotted cache");

    var divergences = rollback.AdvanceAndCompare(ReplayTicks, Authoritative);
    Report(divergences, ReplayTicks, WarmupTicks);

    divergences.Should().BeEmpty(
      "resimulating a rolled-back tick range must reproduce the server's state exactly. A " +
      "divergence here means a system carried per-tick state in a plain field instead of in " +
      "frame state, so the discarded prediction branch leaked into the replay.");
  }

  // Skill state across a rollback boundary. Cooldown counters are the shape that fails here: written
  // by a command on one tick, decremented every tick after, so a client that predicts a cast, rolls
  // it back, and resimulates has to land on the server's remaining ticks exactly.
  //
  // The point pool is granted up front to both sims identically. Without it a hero has only the one
  // point it spawned with, so every upgrade after the first is rejected for lack of points and the
  // mispredicted branch quietly becomes a no-op — the test would pass while proving nothing.
  [Fact]
  public void Rollback_AfterMispredictedSkillUse_ReplayMatchesServerHashes() {
    var rollback = RollbackHarness.Create();
    rollback.Advance(1, beforeTick: sim => GrantSkillPoints(sim, SkillPointPool));

    rollback.Advance(WarmupTicks, AuthoritativeSkills);
    AssertSkillsAreInPlay(rollback.Server);
    rollback.InSync.Should().BeTrue("the two sims must agree before the rollback");

    rollback.MispredictAndRollback(MispredictedTicks, MispredictedSkills);
    rollback.InSync.Should().BeTrue("restoring the snapshot should put the client back on the server");

    var divergences = rollback.AdvanceAndCompare(ReplayTicks, AuthoritativeSkills);
    Report(divergences, ReplayTicks, WarmupTicks);

    divergences.Should().BeEmpty(
      "skill points, ranks, and cooldowns all live on SkillsComponent, so a rollback must restore " +
      "them with the rest of the frame. A divergence here means skill state was cached on a system " +
      "or a behavior instead, and the discarded prediction branch leaked into the replay.");
  }

  // Skill projectiles across a rollback boundary — the shape SkillsComponent's cooldowns are not.
  // A cast creates entities that keep moving on their own for ~50 ticks afterwards, so the snapshot
  // has to restore a set of live projectiles mid-flight, at the right positions, with the right
  // distance left to run. A projectile tracked in a list on ProjectileSystem instead of on its own
  // entity would survive the restore and the client would resimulate against bullets the server
  // never had.
  //
  // The discarded branch sends both heroes the other way, so the units near each in-flight bullet
  // differ between the branch and the replay. That is what makes the hit tests during the discarded
  // ticks genuinely different work rather than the same work twice.
  [Fact]
  public void Rollback_WithProjectilesInFlight_ReplayMatchesServerHashes() {
    var rollback = RollbackHarness.Create(spawnHeroesNow: false);
    rollback.Advance(1, CrystalWarriorPicks);
    rollback.Advance(1, beforeTick: sim => GrantSkillPoints(sim, SkillPointPool));

    rollback.Advance(ProjectileWarmupTicks, AuthoritativeBullets);
    AssertProjectilesAreInFlight(rollback.Server);
    rollback.InSync.Should().BeTrue("the two sims must agree before the rollback");

    rollback.MispredictAndRollback(MispredictedTicks, Mispredicted);
    rollback.InSync.Should().BeTrue("restoring the snapshot should put the client back on the server");

    var divergences = rollback.AdvanceAndCompare(ReplayTicks, AuthoritativeBullets);
    Report(divergences, ReplayTicks, ProjectileWarmupTicks);

    divergences.Should().BeEmpty(
      "a projectile's position, remaining range, and id all live on its own entity, so a rollback " +
      "restores them with the rest of the frame. A divergence here means projectile state was " +
      "cached on ProjectileSystem instead, and the discarded prediction branch leaked into the replay.");
  }

  // Timed stat buffs across a rollback boundary. A buff is two writes that must stay paired - the
  // amount added to StatsComponent and the entry on StatBuffsComponent that owes it back - so a
  // restore that reached one and not the other would leave a hero permanently hardened or hand back
  // an amount it was never given. The discarded branch stacks a buff of its own so the snapshot has
  // both a stat value and an entry the replay must not inherit.
  [Fact]
  public void Rollback_AfterMispredictedBuff_ReplayMatchesServerHashes() {
    var rollback = RollbackHarness.Create(spawnHeroesNow: false);
    rollback.Advance(1, CrystalWarriorPicks);
    rollback.Advance(1, beforeTick: sim => GrantSkillPoints(sim, SkillPointPool));

    rollback.Advance(WarmupTicks, AuthoritativeBuffs);
    AssertBuffsAreActive(rollback.Server);
    rollback.InSync.Should().BeTrue("the two sims must agree before the rollback");

    var armorAtSnapshot = HeroArmor(rollback.Client);
    var armorInBranch = FP64.Zero;
    rollback.MispredictAndRollback(MispredictedTicks, Mispredicted, beforeTick: sim => {
      ApplyBranchBuff(sim);
      armorInBranch = HeroArmor(sim);
    });

    // Without this the branch could be a no-op and the test would pass while proving nothing.
    armorInBranch.Should().BeGreaterThan(armorAtSnapshot, "the discarded branch has to move the stat");
    HeroArmor(rollback.Client).Should().Be(armorAtSnapshot, "the rollback should undo that buff");
    rollback.InSync.Should().BeTrue("restoring the snapshot should put the client back on the server");

    var divergences = rollback.AdvanceAndCompare(ReplayTicks, AuthoritativeBuffs);
    Report(divergences, ReplayTicks, WarmupTicks);

    divergences.Should().BeEmpty(
      "a buff's applied amount and its expiry tick live on StatBuffsComponent, so a rollback " +
      "restores them with the stat they moved. A divergence here means buff state was tracked " +
      "on TimedEffectSystem instead, and the discarded prediction branch leaked into the replay.");
  }

  // Both players rank up and cast Harden every tick; the rank cap and the cooldown do the filtering.
  private static ICommand[] AuthoritativeBuffs(int tick) {
    return [
      SimHarness.UpgradeSkillCommand(playerId: 1, tick, (int)SkillSlot.Secondary),
      SimHarness.CastSkillCommand(playerId: 1, tick, (int)SkillSlot.Secondary),
      SimHarness.UpgradeSkillCommand(playerId: 2, tick, (int)SkillSlot.Secondary),
      SimHarness.CastSkillCommand(playerId: 2, tick, (int)SkillSlot.Secondary),
    ];
  }

  // A second buff source on the same stat, so the branch stacks an entry beside the one Harden is
  // already holding rather than refreshing it.
  private static void ApplyBranchBuff(SimHarness harness) {
    var frame = harness.Frame;
    var filter = frame.Filter<Hero, StatsComponent>();
    while (filter.Next(out var entity))
      StatBuffApplication.ApplyPercent(ref frame, entity, Assets.AssetIds.SkillCrystalGiantUltimate,
        StatType.Armor, FP64.FromInt(1) / FP64.FromInt(2), durationTicks: 600);
  }

  private static FP64 HeroArmor(SimHarness harness) {
    var frame = harness.Frame;
    var hero = harness.FindHero(1);
    return frame.GetReadOnly<StatsComponent>(hero).Armor;
  }

  // Guards the scenario above: a snapshot taken with no buff running would restore nothing
  // interesting.
  private static void AssertBuffsAreActive(SimHarness harness) {
    var frame = harness.Frame;
    StatBuffApplication.ActiveCount(ref frame, harness.FindHero(1)).Should().BeGreaterThan(0,
      "the warmup stream must leave a buff running at the snapshot tick for the rollback to have " +
      "any buff state to restore");
  }

  // Guards the scenario above: a rollback that lands between volleys would restore nothing
  // interesting and the test would pass while proving nothing.
  private static void AssertProjectilesAreInFlight(SimHarness harness) {
    harness.Count<Projectile>().Should().BeGreaterThan(0,
      "the warmup stream must leave bullets in the air at the snapshot tick for the rollback to " +
      "have any projectile state to restore");
  }

  // Both players rank up and fire Crystal Bullets every tick. The rank cap and the cooldown do the
  // filtering, so a volley actually launches once per cooldown, identically on both sims.
  private static ICommand[] AuthoritativeBullets(int tick) {
    return [
      SimHarness.UpgradeSkillCommand(playerId: 1, tick, (int)SkillSlot.Tertiary),
      SimHarness.CastSkillCommand(playerId: 1, tick, (int)SkillSlot.Tertiary,
        FP64.FromDouble(20.0), FP64.FromDouble(0.0)),
      SimHarness.UpgradeSkillCommand(playerId: 2, tick, (int)SkillSlot.Tertiary),
      SimHarness.CastSkillCommand(playerId: 2, tick, (int)SkillSlot.Tertiary,
        FP64.FromDouble(-20.0), FP64.FromDouble(0.0)),
    ];
  }

  // Crystal Giant is the only hero with a skill that fires anything, and the harness defaults every
  // player to Hairy Wizards, so the faction has to be picked explicitly.
  private static ICommand[] CrystalWarriorPicks(int tick) {
    return [
      SimHarness.SelectFactionCommand(playerId: 1, tick, Assets.AssetIds.FactionCrystalWarriors),
      SimHarness.SelectFactionCommand(playerId: 2, tick, Assets.AssetIds.FactionCrystalWarriors),
    ];
  }

  // Guards the scenario above: if the authoritative stream is not actually ranking skills up and
  // leaving a cooldown mid-burn at the snapshot tick, the rollback it wraps proves nothing.
  private static void AssertSkillsAreInPlay(SimHarness harness) {
    var frame = harness.Frame;
    var hero = harness.FindHero(1);
    var skill = harness.AssetRegistry.Get<Assets.SkillAsset>(
      frame.GetReadOnly<SkillsComponent>(hero).GetSkillAssetId((int)SkillSlot.Primary));

    ref readonly var skills = ref frame.GetReadOnly<SkillsComponent>(hero);
    skills.GetRank((int)SkillSlot.Primary).Should().Be(skill.MaxRank,
      "the warmup stream should have ranked Primary all the way up");
    skills.GetCooldownRemainingTicks((int)SkillSlot.Primary).Should().BeGreaterThan(0,
      "a cooldown must still be burning at the snapshot tick for the rollback to have to restore one");
    skills.GetRank((int)SkillSlot.Ultimate).Should().Be(0,
      "Ultimate is the slot the mispredicted branch touches, so it must be untouched here");
  }

  // Stat state across a rollback boundary. StatsComponent is a fixed buffer of raw FP64 values
  // rather than named fields, so the generated codec has to walk it for both the snapshot and the
  // hash - a buffer the generator skipped would restore as zeroes and desync silently. Levelling
  // during the discarded branch is what puts a value in there that the replay must not inherit.
  [Fact]
  public void Rollback_AfterMispredictedLevelUps_ReplayMatchesServerHashes() {
    var rollback = RollbackHarness.Create();

    rollback.Advance(WarmupTicks, Authoritative);
    rollback.InSync.Should().BeTrue("the two sims must agree before the rollback");

    var levelInBranch = 0;
    var maxLevel = rollback.Client.AssetRegistry.Get<XpRulesAsset>().MaxLevel;

    // Levels land during the branch the client throws away, so the stats the replay resumes from
    // have to come out of the snapshot rather than out of the discarded prediction.
    rollback.MispredictAndRollback(MispredictedTicks, Mispredicted, beforeTick: sim => {
      GrantExperience(sim, LevelUpExperience);
      levelInBranch = HeroLevel(sim);
    });

    // Without this the grant could be a no-op and the test would pass while proving nothing.
    levelInBranch.Should().Be(maxLevel, "the discarded branch has to actually move the stats");
    HeroLevel(rollback.Client).Should().Be(1, "the rollback should undo those levels");
    rollback.InSync.Should().BeTrue(
      "restoring the snapshot should undo every stat the discarded branch granted");

    var divergences = rollback.AdvanceAndCompare(ReplayTicks, Authoritative);
    Report(divergences, ReplayTicks, WarmupTicks);

    divergences.Should().BeEmpty(
      "every stat lives in StatsComponent's value buffer, so a rollback must restore the whole " +
      "buffer with the rest of the frame. A divergence here means the buffer is not being " +
      "snapshotted or hashed, and level-up gains from the discarded branch leaked into the replay.");
  }

  private static int HeroLevel(SimHarness harness) {
    var frame = harness.Frame;
    var filter = frame.Filter<Hero, ExperienceComponent>();
    return filter.Next(out var entity) ? frame.GetReadOnly<ExperienceComponent>(entity).Level : 0;
  }

  private static void GrantExperience(SimHarness harness, int experience) {
    var frame = harness.Frame;
    var filter = frame.Filter<Hero, ExperienceComponent>();
    while (filter.Next(out var entity))
      frame.Get<ExperienceComponent>(entity).Experience = experience;
  }

  private static void GrantSkillPoints(SimHarness harness, int points) {
    var frame = harness.Frame;
    var filter = frame.Filter<Hero, SkillsComponent>();
    while (filter.Next(out var entity))
      frame.Get<SkillsComponent>(entity).SkillPoints = points;
  }

  // Regression test for NavigationAgentSystem's navmesh-snap bookkeeping.
  //
  // SyncAgentPosition skips the navmesh snap while an agent has drifted less than
  // PositionSnapThreshold (0.1) from where it was last snapped, and that choice sets nav.Position,
  // which becomes transform.Position. The last-snapped position therefore has to roll back with
  // everything else; it lives in the NavSnapTracker component for that reason. It used to be a
  // plain FPVector3[] field on the system, which Rollback does not restore — the client compared
  // against positions from the discarded branch and snapped on different ticks than the server,
  // and every one of the 40 replayed ticks below diverged.
  //
  // The branch is only observable when the incoming position is genuinely off the navmesh: for an
  // on-mesh point ClosestPointOnNavMesh returns it unchanged and reports the triangle the agent
  // already holds, so snapping and skipping agree. Measured over a 4000-tick run of the shipped
  // map — 96k agent samples — every position was on-mesh and every snap was a no-op, which is why
  // the broad test above did not catch this.
  //
  // So this test supplies the missing condition, which is the one the snap guard exists for in the
  // first place: a unit displaced by something other than nav movement. It walks a hero off the
  // mesh in steps under the threshold. The pocket it walks into is real map geometry — team 1's
  // hero spawn marker (-33.534, 32.731) sits ~0.25 off the baked navmesh, which is why heroes get
  // snapped to (-33.774, 32.810) on their first simulated tick.
  [Fact]
  public void Rollback_WithSubThresholdDrift_ReplayMatchesServer() {
    var rollback = RollbackHarness.Create();
    rollback.Advance(DriftWarmupTicks);

    var step = OffMeshDriftStep(rollback.Server);
    _output.WriteLine($"drift step per tick = ({step.x.ToDouble():F4}, {step.z.ToDouble():F4})");

    rollback.MispredictAndRollback(MispredictedTicks, Mispredicted);
    rollback.InSync.Should().BeTrue("the restored frame should match the server");

    // Both sides now get byte-identical treatment: same displacement, same (empty) command
    // stream, same starting frame.
    var divergences = rollback.AdvanceAndCompare(
      DriftTicks, beforeTick: sim => Displace(sim, playerId: 1, step));

    if (divergences.Count > 0) {
      Report(divergences, DriftTicks, DriftWarmupTicks);
      _output.WriteLine($"server hero: {HeroSample(rollback.Server, 1)}");
      _output.WriteLine($"client hero: {HeroSample(rollback.Client, 1)}");
    }

    divergences.Should().BeEmpty(
      "the last-snapped position must roll back with the frame. If it is moved back onto the " +
      "system as a plain field, Rollback leaves it holding positions from the mispredicted " +
      "branch, the replay takes the snap/skip branch on different ticks than the server did, and " +
      "the hero lands somewhere the server never put it.");
  }

  // Same scenario, reported per unit instead of as one hash, so a failure names the entity and the
  // field rather than just "the frame differs".
  [Fact]
  public void Rollback_WithSubThresholdDrift_NavAgentsMatchServer() {
    var rollback = RollbackHarness.Create();
    rollback.Advance(DriftWarmupTicks);

    var step = OffMeshDriftStep(rollback.Server);

    rollback.MispredictAndRollback(MispredictedTicks, Mispredicted);
    rollback.Advance(DriftTicks, beforeTick: sim => Displace(sim, playerId: 1, step));

    var expected = SnapshotNavAgents(rollback.Server);
    var actual = SnapshotNavAgents(rollback.Client);

    foreach (var kvp in expected)
      if (!actual.TryGetValue(kvp.Key, out var mine) || mine != kvp.Value)
        _output.WriteLine($"unit {kvp.Key}: server={kvp.Value} client={mine}");

    actual.Should().Equal(expected,
      "after rolling back and resimulating the same ticks, every nav agent must end on the " +
      "server's position and navmesh triangle");
  }

  // Control for the two tests above. Identical drift scenario, identical snapshot + rollback — but
  // the client never simulates a mispredicted branch, so there is nothing stale for the rollback
  // to leave behind. This passed even while the other two failed, which is what pinned those
  // failures on the discarded branch specifically rather than on the drift setup, the
  // snapshot/restore path, or running two sims side by side.
  [Fact]
  public void Rollback_WithoutMisprediction_SubThresholdDriftMatchesServer() {
    var rollback = RollbackHarness.Create();
    rollback.Advance(DriftWarmupTicks);

    var step = OffMeshDriftStep(rollback.Server);
    rollback.MispredictAndRollback(ticks: 0, commands: null);

    var divergences = rollback.AdvanceAndCompare(
      DriftTicks, beforeTick: sim => Displace(sim, playerId: 1, step));

    divergences.Should().BeEmpty(
      "a rollback that discards nothing must be a no-op — if this fails, the problem is in the " +
      "snapshot/restore path itself, not in state left over from a mispredicted branch");
  }

  private void Report(IReadOnlyList<int> divergences, int total, int startTick) {
    if (divergences.Count == 0)
      return;

    _output.WriteLine(
      $"first divergence at tick {divergences[0]} ({divergences[0] - startTick} ticks into the " +
      $"replay); {divergences.Count}/{total} replayed ticks diverged");
  }

  // A per-tick displacement small enough to stay under NavigationTuningAsset.PositionSnapThreshold
  // (0.1), pointed from the hero's snapped resting position back at its off-mesh spawn marker.
  // Under the threshold the agent is left unsnapped, so it drifts off the navmesh and only gets
  // pulled back on the ticks where accumulated drift trips the threshold — which is the decision
  // the last-snapped position owns.
  private static FPVector3 OffMeshDriftStep(SimHarness harness) {
    var frame = harness.Frame;
    var spawnMarker = SimulationSetup.GetHeroSpawnPositionForTeam(ref frame, teamId: 1);
    var heroPosition = HeroPosition(harness, playerId: 1);

    var toMarker = new FPVector2(spawnMarker.x - heroPosition.x, spawnMarker.z - heroPosition.z);
    var direction = toMarker.normalized;
    var stepLength = FP64.FromDouble(0.06);

    return new FPVector3(direction.x * stepLength, FP64.Zero, direction.y * stepLength);
  }

  private static FPVector3 HeroPosition(SimHarness harness, int playerId) {
    var frame = harness.Frame;
    var filter = frame.Filter<Hero, TransformComponent>();
    while (filter.Next(out var entity)) {
      ref readonly var hero = ref frame.GetReadOnly<Hero>(entity);
      if (hero.PlayerId != playerId)
        continue;

      return frame.GetReadOnly<TransformComponent>(entity).Position;
    }

    throw new Xunit.Sdk.XunitException($"no hero found for player {playerId}");
  }

  // Nudges a hero's transform directly. Stands in for any system that displaces a unit without
  // routing through nav movement (knockback, pull, teleport) — the reason SyncAgentPosition
  // re-snaps at all.
  private static void Displace(SimHarness harness, int playerId, FPVector3 step) {
    var frame = harness.Frame;
    var filter = frame.Filter<Hero, TransformComponent>();
    while (filter.Next(out var entity)) {
      ref readonly var hero = ref frame.GetReadOnly<Hero>(entity);
      if (hero.PlayerId != playerId)
        continue;

      ref var transform = ref frame.Get<TransformComponent>(entity);
      transform.Position += step;
      return;
    }

    throw new Xunit.Sdk.XunitException($"no hero found for player {playerId}");
  }

  // UnitId → nav agent state. Keyed by the stable gameplay id rather than the ECS entity so a
  // spawn-order difference shows up as a missing key instead of a silent mismatch.
  private static SortedDictionary<int, NavAgentSample> SnapshotNavAgents(SimHarness harness) {
    var result = new SortedDictionary<int, NavAgentSample>();
    var frame = harness.Frame;

    var filter = frame.Filter<UnitIdComponent, NavAgentComponent, TransformComponent>();
    while (filter.Next(out var entity)) {
      ref readonly var unit = ref frame.GetReadOnly<UnitIdComponent>(entity);
      ref readonly var nav = ref frame.GetReadOnly<NavAgentComponent>(entity);
      ref readonly var transform = ref frame.GetReadOnly<TransformComponent>(entity);

      result[unit.UnitId] = new NavAgentSample(
        nav.Position.x, nav.Position.z, nav.CurrentTriangleIndex,
        transform.Position.x, transform.Position.z);
    }

    return result;
  }

  private static string HeroSample(SimHarness harness, int playerId) {
    var frame = harness.Frame;
    var filter = frame.Filter<Hero, NavAgentComponent, TransformComponent>();
    while (filter.Next(out var entity)) {
      ref readonly var hero = ref frame.GetReadOnly<Hero>(entity);
      if (hero.PlayerId != playerId)
        continue;

      ref readonly var nav = ref frame.GetReadOnly<NavAgentComponent>(entity);
      ref readonly var transform = ref frame.GetReadOnly<TransformComponent>(entity);
      return new NavAgentSample(
        nav.Position.x, nav.Position.z, nav.CurrentTriangleIndex,
        transform.Position.x, transform.Position.z).ToString();
    }

    return "<missing>";
  }

  // Authoritative stream: the heroes cross to the opposite side lane, so they spend the run
  // traversing the navmesh rather than sitting on their spawn triangle. Targets are picked on
  // walkable, reachable geometry — the map centre (0, 0) is a hole in the baked mesh, and a move
  // order to an unreachable point leaves the agent in PathFailed, going nowhere.
  private static ICommand[] Authoritative(int tick) {
    return [
      SimHarness.MoveCommand(playerId: 1, tick, FP64.FromDouble(20.0), FP64.FromDouble(0.0)),
      SimHarness.MoveCommand(playerId: 2, tick, FP64.FromDouble(-20.0), FP64.FromDouble(0.0)),
    ];
  }

  // The branch the client predicts and then discards: both heroes head the other way. Far enough
  // from where the replay resumes that any leftover per-agent bookkeeping is clearly stale rather
  // than coincidentally close.
  private static ICommand[] Mispredicted(int tick) {
    return [
      SimHarness.MoveCommand(playerId: 1, tick, FP64.FromDouble(-20.0), FP64.FromDouble(-30.0)),
      SimHarness.MoveCommand(playerId: 2, tick, FP64.FromDouble(20.0), FP64.FromDouble(30.0)),
    ];
  }

  // Both players rank up and cast Primary on a slow, steady cadence — slow enough that the cooldown
  // is genuinely mid-burn when the rollback lands rather than always at zero.
  private static ICommand[] AuthoritativeSkills(int tick) {
    if (tick % 40 == 0)
      return [
        SimHarness.UpgradeSkillCommand(playerId: 1, tick, (int)SkillSlot.Primary),
        SimHarness.UpgradeSkillCommand(playerId: 2, tick, (int)SkillSlot.Primary),
      ];

    if (tick % 40 == 20)
      return [
        SimHarness.CastSkillCommand(playerId: 1, tick, (int)SkillSlot.Primary),
        SimHarness.CastSkillCommand(playerId: 2, tick, (int)SkillSlot.Primary),
      ];

    return [];
  }

  // The discarded branch: both players pour points into Ultimate and cast it, a slot the
  // authoritative stream never ranks at all. Its rank and cooldown are unambiguously state that only
  // ever existed on the branch being thrown away.
  private static ICommand[] MispredictedSkills(int tick) {
    return [
      SimHarness.UpgradeSkillCommand(playerId: 1, tick, (int)SkillSlot.Ultimate),
      SimHarness.CastSkillCommand(playerId: 1, tick, (int)SkillSlot.Ultimate),
      SimHarness.UpgradeSkillCommand(playerId: 2, tick, (int)SkillSlot.Ultimate),
      SimHarness.CastSkillCommand(playerId: 2, tick, (int)SkillSlot.Ultimate),
    ];
  }

  private readonly record struct NavAgentSample(
    FP64 NavX, FP64 NavZ, int TriangleIndex, FP64 TransformX, FP64 TransformZ) {
    public override string ToString() =>
      $"nav=({NavX.ToDouble():F4}, {NavZ.ToDouble():F4}) tri={TriangleIndex} " +
      $"transform=({TransformX.ToDouble():F4}, {TransformZ.ToDouble():F4})";
  }
}
