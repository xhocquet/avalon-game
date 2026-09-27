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

// Pickle Knight's Ultimate, and with it the silence lifecycle nothing else uses: a hold that takes
// skill casts away and nothing else, checked in SkillActions.EvaluateCast rather than by zeroing a
// stat. The cast is a self-cast disc that heals the caster and allies it catches and silences the
// hostiles in the same reach.
//
// Dummies are hand-built the way the other suites build them; the enemy hero is walked onto the
// caster by hand because the two spawn points are a map apart.
public class ExploosionTests {
  private const int CasterPlayerId = 1;
  private const int EnemyPlayerId = 2;
  private const int CasterTeamId = 1;
  private const int EnemyTeamId = 2;
  private const int Ultimate = (int)SkillSlot.Ultimate;
  private const int Tertiary = (int)SkillSlot.Tertiary;

  // The whole skill is authored in one row: it is self-cast, has a disc, heals a fraction of max
  // health, and silences for a flat window. A hardcoded 5 units or 4s fails here rather than shipping.
  [Fact]
  public void EveryNumber_TracesBackToTheAssetRow() {
    var harness = CreatePickleKnightHarness();
    var skill = ExploosionAsset(harness);

    skill.SelfCast.Should().BeTrue();
    skill.HasArea.Should().BeTrue();
    skill.HasSilence.Should().BeTrue();
    skill.SilenceDurationMs.Should().BePositive();
    skill.SilenceDurationMsAtRank(1).Should().Be(skill.SilenceDurationMs);
    skill.SilenceDurationMsAtRank(3).Should().Be(skill.SilenceDurationMs + skill.SilenceDurationMsPerRank * 2);
    SkillAsset.AtRank(skill.HealPercent, skill.HealPercentPerRank, 2).Should().Be(skill.HealPercent + skill.HealPercentPerRank);
  }

  [Fact]
  public void Cast_HealsTheCasterAndEveryAllyInsideTheDisc() {
    var harness = CreatePickleKnightHarness();
    var skill = ExploosionAsset(harness);
    var origin = LearnPosition(harness);

    var near = SpawnDummy(harness, Ahead(origin, 3), CasterTeamId, maxHealth: 400, current: 100);
    var far = SpawnDummy(harness, Ahead(origin, 12), CasterTeamId, maxHealth: 400, current: 100);
    SetCasterHealth(harness, FP64.FromInt(1));

    Cast(harness);

    var fraction = SkillAsset.AtRank(skill.HealPercent, skill.HealPercentPerRank, 1);
    Health(harness, near).Should().Be(FP64.FromInt(100) + FP64.FromInt(400) * fraction);
    Health(harness, far).Should().Be(FP64.FromInt(100), "past the disc");
    CasterHealth(harness).Should().Be(FP64.One + MaxHealth(harness, Caster(harness)) * fraction);
  }

  [Fact]
  public void Cast_SilencesEveryHostileInsideTheDisc_AndLeavesAlliesAndStructuresAlone() {
    var harness = CreatePickleKnightHarness();
    var skill = ExploosionAsset(harness);
    var enemyHero = harness.FindHero(EnemyPlayerId);

    StandEnemyOnCaster(harness);
    var origin = LearnPosition(harness);
    var friendly = SpawnDummy(harness, origin, CasterTeamId, maxHealth: 400, current: 400);
    var structure = SpawnStructure(harness, origin, EnemyTeamId);
    var farFoe = SpawnDummy(harness, Ahead(origin, 12), EnemyTeamId, maxHealth: 400, current: 400);

    var castTick = Cast(harness);

    var silence = harness.Frame.GetReadOnly<Silence>(enemyHero);
    silence.SourceId.Should().Be(AssetIds.SkillPickleKnightUltimate);
    silence.ExpiryTick.Should().Be(castTick + Ticks(harness, skill.SilenceDurationMsAtRank(1)));

    IsSilenced(harness, friendly).Should().BeFalse();
    IsSilenced(harness, structure).Should().BeFalse();
    IsSilenced(harness, farFoe).Should().BeFalse();
  }

  // The silence is enforced in one spot: EvaluateCast. A silenced hero's own skill is refused before
  // the cooldown starts, so it is not spent, and the slot frees itself the tick the hold lifts.
  [Fact]
  public void ASilencedHero_CannotCastUntilItWearsOff() {
    var harness = CreatePickleKnightHarness();
    var skill = ExploosionAsset(harness);

    Tick(harness, SimHarness.UpgradeSkillCommand(EnemyPlayerId, 0, Tertiary));
    var silenceTicks = Ticks(harness, skill.SilenceDurationMsAtRank(1));
    var appliedTick = harness.Frame.Tick;
    ApplySilence(harness, harness.FindHero(EnemyPlayerId), silenceTicks);

    var frame = harness.Frame;
    SkillActions.CastBlock(ref frame, EnemyPlayerId, Tertiary).Should().Be(SkillActions.SkillBlock.Silenced);
    SkillActions.CanCast(ref frame, EnemyPlayerId, Tertiary).Should().BeFalse();
    SkillActions.TryCast(ref frame, EnemyPlayerId, Tertiary, EnemyPosition(harness)).Should().BeFalse();
    EnemyCooldown(harness, Tertiary).Should().Be(0, "a refused cast never starts the cooldown");

    AdvanceTo(harness, appliedTick + silenceTicks - 1);
    CanEnemyCast(harness, Tertiary).Should().BeFalse();

    AdvanceTo(harness, appliedTick + silenceTicks);
    CanEnemyCast(harness, Tertiary).Should().BeTrue();
  }

  // A silence takes casts and nothing else: the hero still walks.
  [Fact]
  public void ASilencedHero_StillMoves() {
    var harness = CreatePickleKnightHarness();
    var origin = HeroPosition(harness);
    ApplySilence(harness, Caster(harness), 600);

    Tick(harness, SimHarness.MoveCommand(CasterPlayerId, 0, origin.x + FP64.FromInt(6), origin.z));
    for (var i = 0; i < 20; i++)
      Tick(harness);

    HeroPosition(harness).x.Should().BeGreaterThan(origin.x);
  }

  [Fact]
  public void TheSilenceHoldsForTheAuthoredWindowThenLiftsExactly() {
    var harness = CreatePickleKnightHarness();
    var enemyHero = harness.FindHero(EnemyPlayerId);

    var appliedTick = harness.Frame.Tick;
    ApplySilence(harness, enemyHero, 40);

    AdvanceTo(harness, appliedTick + 40 - 1);
    IsSilenced(harness, enemyHero).Should().BeTrue();

    AdvanceTo(harness, appliedTick + 40);
    IsSilenced(harness, enemyHero).Should().BeFalse();
  }

  [Fact]
  public void DeathClearsTheSilence() {
    var harness = CreatePickleKnightHarness();
    var enemyHero = harness.FindHero(EnemyPlayerId);
    ApplySilence(harness, enemyHero, 600);

    harness.Frame.Get<Health>(enemyHero).Current = FP64.Zero;
    Tick(harness); // RespawnSystem picks the death up

    IsSilenced(harness, enemyHero).Should().BeFalse();
  }

  // --- setup helpers ---

  private static SimHarness CreatePickleKnightHarness() {
    var harness = SimHarness.CreateInitialized(spawnHeroesNow: false);
    harness.AssetRegistry.Get<WaveRulesAsset>().MinionsPerWave = 0;

    harness.Tick(
      SimHarness.SelectFactionCommand(1, 0, AssetIds.FactionPickleKnights),
      SimHarness.SelectFactionCommand(2, 0, AssetIds.FactionPickleKnights));
    DisableAutoAttacks(harness);
    return harness;
  }

  private static void Tick(SimHarness harness, params ICommand[] commands) {
    harness.Tick(commands);
    DisableAutoAttacks(harness);
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

  // Learns the Ultimate and returns the caster position the disc centres on, read past the tick the
  // hero snaps onto the navmesh.
  private static FPVector3 LearnPosition(SimHarness harness) {
    Tick(harness, SimHarness.UpgradeSkillCommand(CasterPlayerId, 0, Ultimate));
    return HeroPosition(harness);
  }

  // Walks the enemy hero onto the caster and gives it a tick to settle onto the mesh.
  private static void StandEnemyOnCaster(SimHarness harness) {
    harness.Frame.Get<TransformComponent>(harness.FindHero(EnemyPlayerId)).Position = HeroPosition(harness);
    Tick(harness);
  }

  // Casts the Ultimate and returns the tick it executes on, which the silence expiry is measured from.
  private static int Cast(SimHarness harness) {
    if (harness.Frame.GetReadOnly<Skills>(Caster(harness)).GetRank(Ultimate) <= 0)
      Tick(harness, SimHarness.UpgradeSkillCommand(CasterPlayerId, 0, Ultimate));

    var castTick = harness.Frame.Tick;
    Tick(harness, SimHarness.CastSkillCommand(CasterPlayerId, 0, Ultimate));
    return castTick;
  }

  private static void ApplySilence(SimHarness harness, EntityRef entity, int durationTicks) {
    var frame = harness.Frame;
    Silences.Apply(ref frame, entity, AssetIds.SkillPickleKnightUltimate, durationTicks).Should().BeTrue();
  }

  private static void AdvanceTo(SimHarness harness, int tick) {
    while (harness.Frame.Tick <= tick)
      Tick(harness);
  }

  private static EntityRef SpawnDummy(SimHarness harness, FPVector3 position, int teamId, int maxHealth,
    int current) {
    var frame = harness.Frame;
    var entity = frame.CreateEntity();

    frame.Add(entity, TransformFactory.At(position));
    frame.Add(entity, new UnitIdentity {
      UnitId = UnitLookup.NextUnitId(ref frame),
      UnitTypeId = SimulationSetup.MinionUnitTypeId
    });
    frame.Add(entity, new Team { TeamId = teamId });
    frame.Add(entity, new Minion { WaveId = 99 });
    frame.Add(entity, new Health(FP64.FromInt(current)));
    frame.Add(entity, Stats.Create().With(StatType.MaxHealth, FP64.FromInt(maxHealth)));

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

    return entity;
  }

  // --- readers ---

  private static SkillAsset ExploosionAsset(SimHarness harness) {
    return harness.AssetRegistry.Get<SkillAsset>(AssetIds.SkillPickleKnightUltimate);
  }

  private static EntityRef Caster(SimHarness harness) {
    return harness.FindHero(CasterPlayerId);
  }

  private static bool IsSilenced(SimHarness harness, EntityRef entity) {
    var frame = harness.Frame;
    return Silences.IsSilenced(ref frame, entity);
  }

  private static bool CanEnemyCast(SimHarness harness, int slot) {
    var frame = harness.Frame;
    return SkillActions.CanCast(ref frame, EnemyPlayerId, slot);
  }

  private static int EnemyCooldown(SimHarness harness, int slot) {
    return harness.Frame.GetReadOnly<Skills>(harness.FindHero(EnemyPlayerId))
      .GetCooldownRemainingTicks(slot);
  }

  private static FPVector3 HeroPosition(SimHarness harness) {
    return harness.Frame.GetReadOnly<TransformComponent>(Caster(harness)).Position;
  }

  private static FPVector3 EnemyPosition(SimHarness harness) {
    return harness.Frame.GetReadOnly<TransformComponent>(harness.FindHero(EnemyPlayerId)).Position;
  }

  private static void SetCasterHealth(SimHarness harness, FP64 current) {
    harness.Frame.Get<Health>(Caster(harness)).Current = current;
  }

  private static FP64 CasterHealth(SimHarness harness) {
    return harness.Frame.GetReadOnly<Health>(Caster(harness)).Current;
  }

  private static FP64 Health(SimHarness harness, EntityRef entity) {
    return harness.Frame.GetReadOnly<Health>(entity).Current;
  }

  private static FP64 MaxHealth(SimHarness harness, EntityRef entity) {
    return harness.Frame.GetReadOnly<Stats>(entity).MaxHealth;
  }

  private static FPVector3 Ahead(FPVector3 origin, int forward) {
    return origin + new FPVector3(FP64.FromInt(forward), FP64.Zero, FP64.Zero);
  }

  private static int Ticks(SimHarness harness, int milliseconds) {
    var frame = harness.Frame;
    return TickMath.MsToTicksCeil(ref frame, milliseconds);
  }
}
