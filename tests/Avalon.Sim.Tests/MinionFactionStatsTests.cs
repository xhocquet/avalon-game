using FluentAssertions;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using Meesles.Avalon.Sim.Factories;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Deterministic.Navigation;
using xpTURN.Klotho.ECS;
using Xunit;

namespace Meesles.Avalon.Sim.Tests;

public class MinionFactionStatsTests {
  [Fact]
  public void SwirlyEye_ClosesInsideTheOldMeleeReach() {
    var harness = SimHarness.CreateInitialized();
    var frame = harness.Frame;
    var shared = frame.AssetRegistry.Get<MinionStatsAsset>();
    var eyeStats = frame.AssetRegistry.Get<MinionStatsAsset>(AssetIds.SwirlyEyeMinionStats);
    var eye = MinionFactory.Spawn(ref frame, eyeStats, FPVector3.Zero, FP64.Zero, 1, 0);
    var other = MinionFactory.Spawn(ref frame, shared, FPVector3.Zero, FP64.Zero, 1, 0);
    var target = MinionFactory.Spawn(ref frame, shared,
      new FPVector3(FP64.FromDouble(1.5), FP64.Zero, FP64.Zero), FP64.Zero, 2, 0);

    CombatRange.IsWithinReach(ref frame, eye, target, out _, out _).Should().BeFalse();
    CombatRange.IsWithinReach(ref frame, other, target, out _, out _).Should().BeTrue();
    frame.Get<TransformComponent>(target).Position =
      new FPVector3(FP64.FromDouble(1.1), FP64.Zero, FP64.Zero);
    CombatRange.IsWithinReach(ref frame, eye, target, out _, out _).Should().BeTrue();
  }

  [Fact]
  public void Waves_UseEachTeamsMinionStats() {
    var harness = SimHarness.CreateInitialized(spawnHeroesNow: false);
    harness.Tick(SimHarness.SelectFactionCommand(1, harness.Frame.Tick, AssetIds.FactionHairyWizards),
      SimHarness.SelectFactionCommand(2, harness.Frame.Tick, AssetIds.FactionSkinwalkerTribe));
    var frame = harness.Frame;
    var rules = frame.AssetRegistry.Get<WaveRulesAsset>();
    frame.Tick = rules.FirstWaveDelayTicks;
    new WaveSpawnSystem().Update(ref frame);

    var counts = new int[3];
    var minions = frame.Filter<Minion, Team, Stats, NavAgentComponent>();
    while (minions.Next(out var minion)) {
      var teamId = frame.GetReadOnly<Team>(minion).TeamId;
      counts[teamId]++;
      var factionId = teamId == 1 ? AssetIds.FactionHairyWizards : AssetIds.FactionSkinwalkerTribe;
      frame.GetReadOnly<Faction>(minion).FactionId.Should().Be(factionId);
      frame.GetReadOnly<Stats>(minion).AttackRange.Should()
        .Be(FP64.FromDouble(teamId == 1 ? 0.2 : 1.0));
      frame.GetReadOnly<NavAgentComponent>(minion).Radius.Should().Be(FP64.FromDouble(0.3));
      frame.GetReadOnly<Stats>(minion).GameplayRadius.Should().Be(FP64.FromDouble(0.48));
    }

    counts[1].Should().Be(rules.MinionsPerWave);
    counts[2].Should().Be(rules.MinionsPerWave);
  }

  [Theory]
  [InlineData(0, 0.2)]
  [InlineData(AssetIds.FactionHairyWizards, 0.2)]
  [InlineData(AssetIds.FactionSnailheads, 1.0)]
  [InlineData(AssetIds.FactionCrystalWarriors, 1.0)]
  [InlineData(AssetIds.FactionSkinwalkerTribe, 1.0)]
  [InlineData(AssetIds.FactionPickleKnights, 1.0)]
  public void DebugSpawns_UseRequestedFactionsRange(int factionId, double range) {
    var harness = SimHarness.CreateInitialized();
    harness.Tick(SimHarness.DebugCommand(1, harness.Frame.Tick, DebugAction.SpawnMinions, 3,
      FP64.FromInt(5), FP64.FromInt(5), factionId));
    var frame = harness.Frame;
    var seen = 0;
    var minions = frame.Filter<Minion, Stats>();
    while (minions.Next(out var minion)) {
      frame.GetReadOnly<Stats>(minion).AttackRange.Should().Be(FP64.FromDouble(range));
      frame.GetReadOnly<Stats>(minion).GameplayRadius.Should().Be(FP64.FromDouble(0.48));
      seen++;
    }
    seen.Should().BeGreaterThan(0);
  }
}
