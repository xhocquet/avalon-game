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

// Crystal Giant's Ultimate, and with it the two lifecycles nothing else uses: the snare, which takes
// movement away without touching a stat, and the charged burst, which is the only effect that resolves
// on a later tick than the cast that armed it.
//
// Dummies are hand-built for the same reason CrystalBulletsTests builds them - a real minion is steered
// mid-test - except where the point is that something can or cannot move, which needs a real hero.
public class ChrysalisTests {
  private const int CasterPlayerId = 1;
  private const int EnemyPlayerId = 2;
  private const int CasterTeamId = 1;
  private const int EnemyTeamId = 2;
  private const int Ultimate = (int)SkillSlot.Ultimate;

  [Fact]
  public void Cast_ArmsAChargeThatPaysOutOnlyWhenItsWindUpIsDone() {
    var harness = CreateCrystalGiantHarness();
    var skill = ChrysalisAsset(harness);

    var castTick = LearnAndCast(harness);
    var enemy = SpawnDummy(harness, HeroPosition(harness), EnemyTeamId, isMinion: true);
    var healthBefore = Health(harness, enemy);

    // Standing in the middle of it the whole time and untouched right up to the last wind-up tick.
    AdvanceTo(harness, castTick + ChargeTicks(harness, skill) - 1);
    Health(harness, enemy).Should().Be(healthBefore);
    IsCharging(harness).Should().BeTrue();

    AdvanceTo(harness, castTick + ChargeTicks(harness, skill));
    Health(harness, enemy).Should().Be(healthBefore - skill.DamageAtRank(1));
    IsCharging(harness).Should().BeFalse();
  }

  // The whole skill is authored in one row: the wind-up, the disc, the hold, the armor spike, and the
  // damage. A hardcoded 3s or 4.5 units fails here rather than quietly shipping.
  [Fact]
  public void EveryNumber_TracesBackToTheAssetRow() {
    var harness = CreateCrystalGiantHarness();
    var skill = ChrysalisAsset(harness);

    skill.ChargeDurationMs.Should().BePositive();
    skill.ChargeRootsItsCaster.Should().BeTrue();
    skill.HasArea.Should().BeTrue();
    skill.IsSelfCast.Should().BeTrue();
    skill.SnareDurationMsAtRank(2).Should().Be(skill.SnareDurationMs + skill.SnareDurationMsPerRank);

    var armorBefore = Stat(harness, StatType.Armor);
    var castTick = LearnAndCast(harness);

    var charge = harness.Frame.GetReadOnly<SkillChargeComponent>(Caster(harness));
    charge.SourceId.Should().Be(AssetIds.SkillCrystalGiantUltimate);
    charge.Damage.Should().Be(skill.DamageAtRank(1));
    charge.Radius.Should().Be(skill.AreaRadius);
    charge.DetonateTick.Should().Be(castTick + ChargeTicks(harness, skill));
    charge.SnareDurationTicks.Should().Be(Ticks(harness, skill.SnareDurationMsAtRank(1)));

    var entry = BuffEntries(harness).Should().ContainSingle().Subject;
    entry.Stat.Should().Be(StatType.Armor);
    entry.Applied.Should().Be(armorBefore * skill.BuffPercentAtRank(1));
    entry.ExpiryTick.Should().Be(castTick + Ticks(harness, skill.BuffDurationMs));
  }

  [Fact]
  public void TheCasterIsRootedForTheWindUpAndWalksAgainOnceItEnds() {
    var harness = CreateCrystalGiantHarness();
    var skill = ChrysalisAsset(harness);

    var castTick = LearnAndCast(harness);
    var origin = HeroPosition(harness);

    // A standing order the whole way through: nothing about the root cancels it, it only stops paying out.
    Tick(harness, SimHarness.MoveCommand(CasterPlayerId, 0, origin.x + FP64.FromInt(6), origin.z));
    AdvanceTo(harness, castTick + ChargeTicks(harness, skill) - 1);
    HeroPosition(harness).Should().Be(origin);
    harness.Frame.Has<UnitMoveTarget>(Caster(harness)).Should().BeTrue("the order outlives the root");

    for (var i = 0; i < 20; i++)
      Tick(harness);

    HeroPosition(harness).x.Should().BeGreaterThan(origin.x);
  }

  [Fact]
  public void TheBurstHoldsWhatItCaughtForTheRowsDurationAndThenLetsGo() {
    var harness = CreateCrystalGiantHarness();
    var skill = ChrysalisAsset(harness);
    var enemyHero = harness.FindHero(EnemyPlayerId);

    var castTick = StandTheEnemyOnTopOfTheCasterAndCast(harness);
    var detonationTick = castTick + ChargeTicks(harness, skill);
    AdvanceTo(harness, detonationTick);

    IsSnared(harness, enemyHero).Should().BeTrue();

    // Ordered away the tick after it was caught, and still standing where the burst left it.
    var held = Position(harness, enemyHero);
    Tick(harness, SimHarness.MoveCommand(EnemyPlayerId, 0, held.x + FP64.FromInt(6), held.z));
    AdvanceTo(harness, detonationTick + SnareTicks(harness, skill) - 1);
    Position(harness, enemyHero).Should().Be(held);

    for (var i = 0; i < 20; i++)
      Tick(harness);

    IsSnared(harness, enemyHero).Should().BeFalse();
    Position(harness, enemyHero).x.Should().BeGreaterThan(held.x);
  }

  [Fact]
  public void OnlyWhatStandsInsideTheDiscIsCaught() {
    var harness = CreateCrystalGiantHarness();
    var skill = ChrysalisAsset(harness);

    var castTick = LearnAndCast(harness);
    var origin = HeroPosition(harness);

    var inside = SpawnDummy(harness, Offset(origin, skill.AreaRadius - FP64.Half), EnemyTeamId, isMinion: true);
    var outside = SpawnDummy(harness, Offset(origin, skill.AreaRadius + FP64.One), EnemyTeamId, isMinion: true);
    var friendly = SpawnDummy(harness, origin, CasterTeamId, isMinion: true);
    var structure = SpawnDummy(harness, origin, EnemyTeamId, isMinion: false);
    var before = new[] { inside, outside, friendly, structure }.Select(e => Health(harness, e)).ToList();

    AdvanceTo(harness, castTick + ChargeTicks(harness, skill));

    Health(harness, inside).Should().Be(before[0] - skill.DamageAtRank(1));
    IsSnared(harness, inside).Should().BeTrue();
    Health(harness, outside).Should().Be(before[1]);
    Health(harness, friendly).Should().Be(before[2]);
    Health(harness, structure).Should().Be(before[3], "structures are excluded from skill hits");
  }

  [Fact]
  public void DyingMidWindUp_TakesTheChargeWithItRatherThanFiringOffACorpse() {
    var harness = CreateCrystalGiantHarness();
    var skill = ChrysalisAsset(harness);

    var castTick = LearnAndCast(harness);
    var enemy = SpawnDummy(harness, HeroPosition(harness), EnemyTeamId, isMinion: true);
    var healthBefore = Health(harness, enemy);

    harness.Frame.Get<Health>(Caster(harness)).Current = FP64.Zero;
    Tick(harness); // RespawnSystem picks the death up

    IsCharging(harness).Should().BeFalse();
    IsSnared(harness, Caster(harness)).Should().BeFalse();

    AdvanceTo(harness, castTick + ChargeTicks(harness, skill));
    Health(harness, enemy).Should().Be(healthBefore);
  }

  [Fact]
  public void Detonation_RaisesOneEventCarryingWhereAndHowWideItLanded() {
    var harness = CreateCrystalGiantHarness();
    var skill = ChrysalisAsset(harness);

    var castTick = LearnAndCast(harness);
    var origin = HeroPosition(harness);
    SpawnDummy(harness, origin, EnemyTeamId, isMinion: true);

    var collector = CollectEvents(harness);
    AdvanceTo(harness, castTick + ChargeTicks(harness, skill));

    var detonated = collector.Collected.OfType<SkillChargeDetonatedEvent>().Should().ContainSingle().Subject;
    detonated.SkillAssetId.Should().Be(AssetIds.SkillCrystalGiantUltimate);
    detonated.CasterUnitId.Should().Be(UnitId(harness, Caster(harness)));
    detonated.Radius.Should().Be(skill.AreaRadius);
    detonated.Position.Should().Be(origin);
    detonated.HitCount.Should().Be(1);
  }

  // --- setup helpers ---

  // The harness defaults every player to Hairy Wizards, whose skill set is empty, so go through the
  // real faction-select path to get a Crystal Giant on the board.
  private static SimHarness CreateCrystalGiantHarness() {
    var harness = SimHarness.CreateInitialized(spawnHeroesNow: false);

    // A three-second wind-up outlasts two wave spawns, and a minion arrives with a fresh Combat and
    // swings on its own spawn tick, before anything outside the sim can strip it. The registry is
    // loaded per harness, so switching the waves off here leaves the burst as the only damage on the
    // board and a health delta means exactly one thing.
    harness.AssetRegistry.Get<WaveRulesAsset>().MinionsPerWave = 0;

    Tick(harness,
      SimHarness.SelectFactionCommand(1, 0, AssetIds.FactionCrystalWarriors),
      SimHarness.SelectFactionCommand(2, 0, AssetIds.FactionCrystalWarriors));

    return harness;
  }

  private static void Tick(SimHarness harness, params ICommand[] commands) {
    harness.Tick(commands);
    DisableAutoAttacks(harness);
  }

  // The heroes and structures already on the board when the harness starts, silenced once.
  private static void DisableAutoAttacks(SimHarness harness) {
    var frame = harness.Frame;
    var attackers = new List<EntityRef>();

    var filter = frame.Filter<Combat>();
    while (filter.Next(out var entity))
      attackers.Add(entity);

    foreach (var entity in attackers)
      frame.Remove<Combat>(entity);
  }

  // Returns the tick the cast executed on, which is what both clocks are measured from.
  private static int LearnAndCast(SimHarness harness) {
    Tick(harness, SimHarness.UpgradeSkillCommand(CasterPlayerId, 0, Ultimate));

    var castTick = harness.Frame.Tick;
    Tick(harness, SimHarness.CastSkillCommand(CasterPlayerId, 0, Ultimate));
    return castTick;
  }

  // The two spawn points are a map apart, so the enemy hero is walked over by hand. Moved before the
  // cast and given a tick to settle, so NavigationAgentSystem has snapped it onto the mesh by the time
  // the disc is measured.
  private static int StandTheEnemyOnTopOfTheCasterAndCast(SimHarness harness) {
    var enemyHero = harness.FindHero(EnemyPlayerId);
    harness.Frame.Get<TransformComponent>(enemyHero).Position = HeroPosition(harness);
    Tick(harness);

    return LearnAndCast(harness);
  }

  private static void AdvanceTo(SimHarness harness, int tick) {
    while (harness.Frame.Tick <= tick)
      Tick(harness);
  }

  private static EntityRef SpawnDummy(SimHarness harness, FPVector3 position, int teamId, bool isMinion) {
    var frame = harness.Frame;
    var entity = frame.CreateEntity();

    frame.Add(entity, TransformFactory.At(position));
    frame.Add(entity, new UnitIdComponent {
      UnitId = UnitLookup.NextUnitId(ref frame),
      UnitTypeId = isMinion ? SimulationSetup.MinionUnitTypeId : SimulationSetup.TurretUnitTypeId
    });
    frame.Add(entity, new TeamComponent(teamId));
    frame.Add(entity, new Health(1500));

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

  // --- readers ---

  private static SkillAsset ChrysalisAsset(SimHarness harness) {
    return harness.AssetRegistry.Get<SkillAsset>(AssetIds.SkillCrystalGiantUltimate);
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

  private static FP64 Stat(SimHarness harness, StatType stat) {
    return harness.Frame.GetReadOnly<StatsComponent>(Caster(harness)).Get(stat);
  }

  private static int UnitId(SimHarness harness, EntityRef entity) {
    return harness.Frame.GetReadOnly<UnitIdComponent>(entity).UnitId;
  }

  private static int Ticks(SimHarness harness, int milliseconds) {
    var frame = harness.Frame;
    return TickMath.MsToTicksCeil(ref frame, milliseconds);
  }

  private static int ChargeTicks(SimHarness harness, SkillAsset skill) {
    return Ticks(harness, skill.ChargeDurationMs);
  }

  private static int SnareTicks(SimHarness harness, SkillAsset skill) {
    return Ticks(harness, skill.SnareDurationMsAtRank(1));
  }

  private readonly record struct BuffEntry(int SourceId, StatType Stat, FP64 Applied, int ExpiryTick);

  private static List<BuffEntry> BuffEntries(SimHarness harness) {
    var entries = new List<BuffEntry>();
    var hero = Caster(harness);
    if (!harness.Frame.Has<StatBuffsComponent>(hero))
      return entries;

    ref readonly var buffs = ref harness.Frame.GetReadOnly<StatBuffsComponent>(hero);
    for (var i = 0; i < StatBuffsComponent.MaxEntries; i++)
      if (buffs.IsActive(i))
        entries.Add(new BuffEntry(buffs.GetSourceId(i), buffs.GetStat(i), buffs.GetApplied(i),
          buffs.GetExpiryTick(i)));

    return entries;
  }
}
