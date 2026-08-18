using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Meesles.Avalon.Sim.Commands;
using Meesles.Avalon.Sim.Components;
using Meesles.Avalon.Sim.Factories;
using Xunit;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Tests;

public class AttackCommandExecutionTests {
  // MinionStatsAsset row 103
  private static readonly FP64 MinionAttackDamage = FP64.FromInt(12);

  [Fact]
  public void CommandFactory_CreatesAttackCommand() {
    WarmupRegistry.RunAll();
    var factory = new CommandFactory();
    factory.CreateCommand(AttackCommand.TYPE_ID).Should().BeOfType<AttackCommand>();
  }

  [Fact]
  public void MissingTarget_NoOps() {
    var harness = SimHarness.CreateInitialized();

    harness.Tick(SimHarness.AttackCommand(1, 0, targetUnitId: 999, sourceUnitIds: 3));

    HasAttackTarget(harness.Frame, unitId: 3).Should().BeFalse();
  }

  [Fact]
  public void DestroyedTarget_NoOps() {
    var harness = SimHarness.CreateInitialized();
    KillUnit(harness, unitId: 2);
    harness.Tick();

    harness.Tick(SimHarness.AttackCommand(1, 1, targetUnitId: 2, sourceUnitIds: 3));

    HasAttackTarget(harness.Frame, unitId: 3).Should().BeFalse();
  }

  [Fact]
  public void NonOwnedSource_NoOps() {
    var harness = SimHarness.CreateInitialized();

    harness.Tick(SimHarness.AttackCommand(1, 0, targetUnitId: 4, sourceUnitIds: 4));

    HasAttackTarget(harness.Frame, unitId: 4).Should().BeFalse();
  }

  [Fact]
  public void SameTeamTarget_NoOps() {
    var harness = SimHarness.CreateInitialized();

    harness.Tick(SimHarness.AttackCommand(1, 0, targetUnitId: 1, sourceUnitIds: 3));

    HasAttackTarget(harness.Frame, unitId: 3).Should().BeFalse();
  }

  [Fact]
  public void ValidAttack_RecordsIntentAndReplacesMoveTargetWithChaseTarget() {
    var harness = SimHarness.CreateInitialized();
    var (source, target) = SpawnFirstWave(harness);

    var move = SimHarness.MoveCommand(1, 0, FP64.One, -FP64.One);
    move.UnitIds.Add(source.UnitId);
    harness.Tick(move);

    HasMoveTarget(harness.Frame, source.UnitId).Should().BeTrue();

    harness.Tick(SimHarness.AttackCommand(1, 1, target.UnitId, source.UnitId));

    GetAttackTarget(harness.Frame, source.UnitId).Should().Be(target.UnitId);
    GetMoveTarget(harness.Frame, source.UnitId).Should().Be(target.Position);
  }

  [Fact]
  public void AttackIntent_UpdatesChaseTargetWhenTargetMoves() {
    var harness = SimHarness.CreateInitialized();
    var (source, target) = SpawnFirstWave(harness);
    harness.Tick(SimHarness.AttackCommand(1, 0, target.UnitId, source.UnitId));

    FPVector3 movedTarget = target.Position + new FPVector3(FP64.FromInt(3), FP64.Zero, FP64.FromInt(-2));
    SetPosition(harness, target.UnitId, movedTarget);

    harness.Tick();

    GetMoveTarget(harness.Frame, source.UnitId).Should().Be(movedTarget);
  }

  [Fact]
  public void AttackIntent_ClearsMoveTargetWhenSourceIsInRange() {
    var harness = SimHarness.CreateInitialized();
    var (source, target) = SpawnFirstWave(harness);
    FPVector3 targetPosition = target.Position;
    SetPosition(harness, source.UnitId, targetPosition + new FPVector3(FP64.One, FP64.Zero, FP64.Zero));
    SetMoveTarget(harness, source.UnitId, FPVector3.Zero);

    harness.Tick(SimHarness.AttackCommand(1, 0, target.UnitId, source.UnitId));

    HasMoveTarget(harness.Frame, source.UnitId).Should().BeFalse();
    GetAttackTarget(harness.Frame, source.UnitId).Should().Be(target.UnitId);
  }

  [Fact]
  public void AttackIntent_ClearsIntentWhenTargetDies() {
    var harness = SimHarness.CreateInitialized();
    var (source, target) = SpawnFirstWave(harness);
    harness.Tick(SimHarness.AttackCommand(1, 0, target.UnitId, source.UnitId));

    KillUnit(harness, target.UnitId);
    harness.Tick();

    HasAttackTarget(harness.Frame, source.UnitId).Should().BeFalse();
  }

  [Fact]
  public void AttackIntent_ClearsIntentWhenTargetDiesEvenWithNearbyEnemy() {
    var harness = SimHarness.CreateInitialized();
    var (source, target) = SpawnFirstWave(harness);
    FPVector3 sourcePosition = new FPVector3(FP64.Zero, FP64.Zero, FP64.Zero);
    FPVector3 targetPosition = new FPVector3(FP64.FromInt(2), FP64.Zero, FP64.Zero);
    FPVector3 fallbackPosition = new FPVector3(FP64.FromInt(5), FP64.Zero, FP64.Zero);
    SetPosition(harness, source.UnitId, sourcePosition);
    SetPosition(harness, target.UnitId, targetPosition);
    SetPosition(harness, unitId: 2, fallbackPosition);
    harness.Tick(SimHarness.AttackCommand(1, 0, target.UnitId, source.UnitId));

    KillUnit(harness, target.UnitId);
    harness.Tick();

    HasAttackTarget(harness.Frame, source.UnitId).Should().BeFalse();
  }

  [Fact]
  public void AttackIntent_ClearsIntentWhenNoReacquireTargetIsInRadius() {
    var harness = SimHarness.CreateInitialized();
    var (source, target) = SpawnFirstWave(harness);
    SetPosition(harness, source.UnitId, OpenGround);
    SetPosition(harness, target.UnitId, EastOfOpenGround(FP64.FromInt(2)));
    SetPosition(harness, unitId: 2, EastOfOpenGround(FP64.FromInt(20)));
    harness.Tick(SimHarness.AttackCommand(1, 0, target.UnitId, source.UnitId));

    KillUnit(harness, target.UnitId);
    harness.Tick();

    HasAttackTarget(harness.Frame, source.UnitId).Should().BeFalse();
  }

  [Fact]
  public void AttackIntent_ClearsIntentForSourceWithoutCombat() {
    var harness = SimHarness.CreateInitialized();
    TryGetEntityByUnitId(harness.Frame, unitId: 3, out var source).Should().BeTrue();
    harness.Frame.Has<Combat>(source).Should().BeTrue();
    harness.Frame.Remove<Combat>(source);

    harness.Tick(SimHarness.AttackCommand(1, 0, targetUnitId: 4, sourceUnitIds: 3));

    HasAttackTarget(harness.Frame, unitId: 3).Should().BeFalse();
  }

  // Unit 2 is a crystal, so its own footprint holes the navmesh underneath it and the order lands on
  // the walkable rim instead of the exact centre (see StructureAttack_* below).
  [Fact]
  public void HeroAttackCommand_SetsMoveTargetImmediately() {
    var harness = SimHarness.CreateInitialized();

    harness.Tick(SimHarness.AttackCommand(1, 0, targetUnitId: 2, sourceUnitIds: 3));

    HasAttackTarget(harness.Frame, unitId: 3).Should().BeTrue();
    FPVector3 targetPosition = GetPosition(harness.Frame, unitId: 2);
    GetMoveTarget(harness.Frame, unitId: 3)
      .Should()
      .Be(SnapToWalkable(harness, new FPVector3(targetPosition.x, FP64.Zero, targetPosition.z)));
  }

  [Fact]
  public void HeroAttackCommand_DamagesEnemyWhenInRange() {
    var harness = SimHarness.CreateInitialized();
    FPVector3 targetPosition = GetPosition(harness.Frame, unitId: 2);
    SetPosition(harness, unitId: 3, targetPosition + new FPVector3(FP64.One, FP64.Zero, FP64.Zero));
    var startHealth = GetHealth(harness.Frame, unitId: 2);
    var attackDamage = GetAttackDamage(harness.Frame, unitId: 3);
    var armor = GetArmor(harness.Frame, unitId: 2);
    var hundred = FP64.FromInt(100);

    harness.Tick(SimHarness.AttackCommand(1, 0, targetUnitId: 2, sourceUnitIds: 3));

    GetHealth(harness.Frame, unitId: 2)
      .Should().Be(startHealth - attackDamage * (hundred / (hundred + armor)));
  }

  [Fact]
  public void DamageSystem_AppliesDamageAndStartsCooldownWhenTargetIsInRange() {
    var harness = SimHarness.CreateInitialized();
    var (source, target) = SpawnFirstWave(harness);
    SetPosition(harness, source.UnitId, target.Position + new FPVector3(FP64.One, FP64.Zero, FP64.Zero));

    var startHealth = GetHealth(harness.Frame, target.UnitId);

    harness.Tick(SimHarness.AttackCommand(1, 0, target.UnitId, source.UnitId));

    // Minions carry no armor, so the whole hit lands. 1.25 attacks/sec over a 16ms tick is 50 ticks.
    GetHealth(harness.Frame, target.UnitId).Should().Be(startHealth - MinionAttackDamage);
    GetCooldown(harness.Frame, source.UnitId).Should().Be(50);
  }

  // Expected damage is authored as a fraction so the case table stays independent of the formula
  // rather than restating it.
  [Theory]
  [InlineData(0, 12, 1)]      // no armor: the attacker's full 12 damage lands
  [InlineData(100, 6, 1)]     // armor equal to the curve constant halves the hit
  [InlineData(10, 1200, 110)] // fractional now, where the int block rounded it to 10
  [InlineData(5000, 1200, 1100)] // authored past the StatRanges ceiling, so it lands on armor 1000
  [InlineData(-100, 18, 1)]   // negative armor amplifies instead of passing damage through raw
  public void DamageSystem_ArmorScalesDamageByAFraction(int armor, int expectedNumerator,
    int expectedDenominator) {
    var harness = SimHarness.CreateInitialized();
    var (source, target) = SpawnFirstWave(harness);
    SetPosition(harness, source.UnitId, target.Position + new FPVector3(FP64.One, FP64.Zero, FP64.Zero));
    SetArmor(harness, target.UnitId, armor);

    var startHealth = GetHealth(harness.Frame, target.UnitId);
    GetAttackDamage(harness.Frame, source.UnitId).Should().Be(MinionAttackDamage);

    harness.Tick(SimHarness.AttackCommand(1, 0, target.UnitId, source.UnitId));

    var expected = FP64.FromInt(expectedNumerator) / FP64.FromInt(expectedDenominator);
    var dealt = startHealth - GetHealth(harness.Frame, target.UnitId);
    FP64.Abs(dealt - expected).Should().BeLessThanOrEqualTo(FP64.FromRaw(4),
      $"expected about {expected} damage but {dealt} landed");
  }

  [Fact]
  public void AttackCooldown_DecrementsAndPreventsRepeatedDamage() {
    var harness = SimHarness.CreateInitialized();
    var (source, target) = SpawnFirstWave(harness);
    SetPosition(harness, source.UnitId, target.Position + new FPVector3(FP64.One, FP64.Zero, FP64.Zero));

    harness.Tick(SimHarness.AttackCommand(1, 0, target.UnitId, source.UnitId));
    var healthAfterFirstHit = GetHealth(harness.Frame, target.UnitId);

    harness.Tick();

    GetHealth(harness.Frame, target.UnitId).Should().Be(healthAfterFirstHit);
    GetCooldown(harness.Frame, source.UnitId).Should().Be(49);
  }

  [Fact]
  public void DamageSystem_LethalDamageLetsDeathSystemRemoveTarget() {
    var harness = SimHarness.CreateInitialized();
    var (source, target) = SpawnFirstWave(harness);
    SetPosition(harness, unitId: 2, new FPVector3(FP64.FromInt(20), FP64.Zero, FP64.Zero));
    SetPosition(harness, unitId: 4, new FPVector3(FP64.FromInt(20), FP64.Zero, FP64.Zero));
    SetPosition(harness, source.UnitId, target.Position + new FPVector3(FP64.One, FP64.Zero, FP64.Zero));
    SetHealth(harness, target.UnitId, 9);

    harness.Tick(SimHarness.AttackCommand(1, 0, target.UnitId, source.UnitId));

    TryGetEntityByUnitId(harness.Frame, target.UnitId, out _).Should().BeFalse();

    harness.Tick();

    HasAttackTarget(harness.Frame, source.UnitId).Should().BeFalse();
  }

  [Fact]
  public void MoveCommand_ClearsAttackIntentAndTransientCombatTarget() {
    var harness = SimHarness.CreateInitialized();
    var (source, target) = SpawnFirstWave(harness);
    SetPosition(harness, source.UnitId, target.Position + new FPVector3(FP64.One, FP64.Zero, FP64.Zero));
    harness.Tick(SimHarness.AttackCommand(1, 0, target.UnitId, source.UnitId));

    HasCombatTarget(harness.Frame, source.UnitId).Should().BeTrue();

    var move = SimHarness.MoveCommand(1, 1, FP64.FromInt(5), FP64.FromInt(-5));
    move.UnitIds.Add(source.UnitId);
    harness.Tick(move);

    HasAttackTarget(harness.Frame, source.UnitId).Should().BeFalse();
    HasCombatTarget(harness.Frame, source.UnitId).Should().BeFalse();
  }

  [Fact]
  public void TargetAcquisition_PrefersEnemyMinionBeforeHeroThenUnitId() {
    var harness = SimHarness.CreateInitialized();
    var (source, target) = SpawnFirstWave(harness);
    int extraMinionUnitId = SpawnTestMinion(harness, teamId: 2,
      EastOfOpenGround(FP64.FromInt(4)));

    SetPosition(harness, source.UnitId, OpenGround);
    SetPosition(harness, unitId: 4, EastOfOpenGround(FP64.One));
    SetPosition(harness, target.UnitId, EastOfOpenGround(FP64.FromInt(3)));
    ClearAttackTargets(harness);

    harness.Tick();

    GetAttackTarget(harness.Frame, source.UnitId).Should().Be(target.UnitId);
    target.UnitId.Should().BeLessThan(extraMinionUnitId);
  }

  [Fact]
  public void TargetAcquisition_DoesNotReplaceExistingAttackTarget() {
    var harness = SimHarness.CreateInitialized();
    var (source, target) = SpawnFirstWave(harness);

    SetPosition(harness, source.UnitId, OpenGround);
    SetPosition(harness, unitId: 4, EastOfOpenGround(FP64.One));
    SetPosition(harness, target.UnitId, EastOfOpenGround(FP64.FromInt(2)));
    ClearAttackTargets(harness);

    harness.Tick(SimHarness.AttackCommand(1, 0, targetUnitId: 4, sourceUnitIds: source.UnitId));
    harness.Tick();

    GetAttackTarget(harness.Frame, source.UnitId).Should().Be(4);
  }

  [Fact]
  public void TurretAcquisition_PrefersEnemyMinionBeforeHeroThenUnitId() {
    var harness = SimHarness.CreateInitialized();
    UnitSnapshot turret = GetTurrets(harness).First(turret => turret.TeamId == 1);
    int enemyMinionUnitId = SpawnTestMinion(harness, teamId: 2,
      new FPVector3(FP64.FromInt(3), FP64.Zero, FP64.Zero));

    SetPosition(harness, turret.UnitId, FPVector3.Zero);
    SetPosition(harness, unitId: 4, new FPVector3(FP64.One, FP64.Zero, FP64.Zero));
    ClearAttackTargets(harness);

    harness.Tick();

    GetAttackTarget(harness.Frame, turret.UnitId).Should().Be(enemyMinionUnitId);
  }

  [Fact]
  public void TurretAttack_DamagesEnemyInRangeWithoutMoveTarget() {
    var harness = SimHarness.CreateInitialized();
    UnitSnapshot turret = GetTurrets(harness).First(turret => turret.TeamId == 1);

    SetPosition(harness, turret.UnitId, FPVector3.Zero);
    SetPosition(harness, unitId: 4, new FPVector3(FP64.FromInt(5), FP64.Zero, FP64.Zero));
    var startHealth = GetHealth(harness.Frame, unitId: 4);
    var turretDamage = GetAttackDamage(harness.Frame, turret.UnitId);
    var armor = GetArmor(harness.Frame, unitId: 4);
    var hundred = FP64.FromInt(100);
    ClearAttackTargets(harness);

    harness.Tick();

    GetHealth(harness.Frame, unitId: 4)
      .Should().Be(startHealth - turretDamage * (hundred / (hundred + armor)));
    HasMoveTarget(harness.Frame, turret.UnitId).Should().BeFalse();
  }

  [Fact]
  public void TurretAttack_DoesNotPathWhenTargetLeavesRange() {
    var harness = SimHarness.CreateInitialized();
    UnitSnapshot turret = GetTurrets(harness).First(turret => turret.TeamId == 1);

    SetPosition(harness, turret.UnitId, FPVector3.Zero);
    SetPosition(harness, unitId: 4, new FPVector3(FP64.FromInt(5), FP64.Zero, FP64.Zero));
    ClearAttackTargets(harness);
    harness.Tick();

    HasAttackTarget(harness.Frame, turret.UnitId).Should().BeTrue();
    SetPosition(harness, unitId: 4, new FPVector3(FP64.FromInt(20), FP64.Zero, FP64.Zero));

    harness.Tick();

    HasAttackTarget(harness.Frame, turret.UnitId).Should().BeFalse();
    HasCombatTarget(harness.Frame, turret.UnitId).Should().BeFalse();
    HasMoveTarget(harness.Frame, turret.UnitId).Should().BeFalse();
  }

  [Fact]
  public void TargetAcquisition_AcquiresEnemyTurret() {
    var harness = SimHarness.CreateInitialized();
    var (source, _) = SpawnFirstWave(harness);
    UnitSnapshot turret = GetTurrets(harness).First(turret => turret.TeamId == 2);

    ScatterHostiles(harness, teamId: 1);
    SetPosition(harness, source.UnitId, OpenGround);
    SetPosition(harness, turret.UnitId, EastOfOpenGround(FP64.FromInt(3)));
    ClearAttackTargets(harness);

    harness.Tick();

    GetAttackTarget(harness.Frame, source.UnitId).Should().Be(turret.UnitId);
  }

  [Fact]
  public void TargetAcquisition_AcquiresEnemyCrystal() {
    var harness = SimHarness.CreateInitialized();
    var (source, _) = SpawnFirstWave(harness);
    UnitSnapshot crystal = GetCrystals(harness).First(crystal => crystal.TeamId == 2);

    ScatterHostiles(harness, teamId: 1);
    SetPosition(harness, source.UnitId, OpenGround);
    SetPosition(harness, crystal.UnitId, EastOfOpenGround(FP64.FromInt(3)));
    ClearAttackTargets(harness);

    harness.Tick();

    GetAttackTarget(harness.Frame, source.UnitId).Should().Be(crystal.UnitId);
  }

  [Fact]
  public void TargetAcquisition_DamagesAcquiredEnemyCrystal() {
    var harness = SimHarness.CreateInitialized();
    var (source, _) = SpawnFirstWave(harness);
    UnitSnapshot crystal = GetCrystals(harness).First(crystal => crystal.TeamId == 2);

    ScatterHostiles(harness, teamId: 1);
    SetPosition(harness, source.UnitId, OpenGround);
    SetPosition(harness, crystal.UnitId, EastOfOpenGround(FP64.One));
    var startHealth = GetHealth(harness.Frame, crystal.UnitId);
    ClearAttackTargets(harness);

    harness.Tick();

    // Crystals carry no armor, so the minion's whole hit lands.
    GetHealth(harness.Frame, crystal.UnitId).Should().Be(startHealth - MinionAttackDamage);
  }

  [Fact]
  public void TargetAcquisition_PrefersNearbyTurretOverNearerCrystal() {
    var harness = SimHarness.CreateInitialized();
    var (source, _) = SpawnFirstWave(harness);
    UnitSnapshot turret = GetTurrets(harness).First(turret => turret.TeamId == 2);
    UnitSnapshot crystal = GetCrystals(harness).First(crystal => crystal.TeamId == 2);

    ScatterHostiles(harness, teamId: 1);
    SetPosition(harness, source.UnitId, OpenGround);
    SetPosition(harness, crystal.UnitId, EastOfOpenGround(FP64.One));
    SetPosition(harness, turret.UnitId, EastOfOpenGround(FP64.FromInt(5)));
    ClearAttackTargets(harness);

    harness.Tick();

    GetAttackTarget(harness.Frame, source.UnitId).Should().Be(turret.UnitId);
  }

  [Fact]
  public void TargetAcquisition_PrefersEnemyHeroOverNearerStructures() {
    var harness = SimHarness.CreateInitialized();
    var (source, _) = SpawnFirstWave(harness);
    UnitSnapshot turret = GetTurrets(harness).First(turret => turret.TeamId == 2);
    UnitSnapshot crystal = GetCrystals(harness).First(crystal => crystal.TeamId == 2);

    ScatterHostiles(harness, teamId: 1);
    SetPosition(harness, source.UnitId, OpenGround);
    SetPosition(harness, crystal.UnitId, EastOfOpenGround(FP64.One));
    SetPosition(harness, turret.UnitId, EastOfOpenGround(FP64.FromInt(2)));
    SetPosition(harness, unitId: 4, EastOfOpenGround(FP64.FromInt(6)));
    ClearAttackTargets(harness);

    harness.Tick();

    GetAttackTarget(harness.Frame, source.UnitId).Should().Be(4);
  }

  [Fact]
  public void TargetAcquisition_IgnoresFriendlyStructures() {
    var harness = SimHarness.CreateInitialized();
    var (source, _) = SpawnFirstWave(harness);
    UnitSnapshot turret = GetTurrets(harness).First(turret => turret.TeamId == 1);
    UnitSnapshot crystal = GetCrystals(harness).First(crystal => crystal.TeamId == 1);

    ScatterHostiles(harness, teamId: 1);
    SetPosition(harness, source.UnitId, OpenGround);
    SetPosition(harness, crystal.UnitId, EastOfOpenGround(FP64.One));
    SetPosition(harness, turret.UnitId, EastOfOpenGround(FP64.FromInt(2)));
    ClearAttackTargets(harness);

    harness.Tick();

    HasAttackTarget(harness.Frame, source.UnitId).Should().BeFalse();
  }

  private static bool HasAttackTarget(Frame frame, int unitId) {
    return TryGetEntityByUnitId(frame, unitId, out var entity)
        && frame.Has<AttackTargetUnitId>(entity);
  }

  private static bool HasMoveTarget(Frame frame, int unitId) {
    return TryGetEntityByUnitId(frame, unitId, out var entity)
        && frame.Has<UnitMoveTarget>(entity);
  }

  private static bool HasCombatTarget(Frame frame, int unitId) {
    return TryGetEntityByUnitId(frame, unitId, out var entity)
        && frame.Has<Combat>(entity)
        && frame.GetReadOnly<Combat>(entity).TargetUnitId != 0;
  }

  private static int GetAttackTarget(Frame frame, int unitId) {
    TryGetEntityByUnitId(frame, unitId, out var entity).Should().BeTrue();
    frame.Has<AttackTargetUnitId>(entity).Should().BeTrue();
    return frame.GetReadOnly<AttackTargetUnitId>(entity).TargetUnitId;
  }

  private static FPVector3 GetMoveTarget(Frame frame, int unitId) {
    TryGetEntityByUnitId(frame, unitId, out var entity).Should().BeTrue();
    frame.Has<UnitMoveTarget>(entity).Should().BeTrue();
    return frame.GetReadOnly<UnitMoveTarget>(entity).Target;
  }

  private static FPVector3 GetPosition(Frame frame, int unitId) {
    TryGetEntityByUnitId(frame, unitId, out var entity).Should().BeTrue();
    frame.Has<TransformComponent>(entity).Should().BeTrue();
    return frame.GetReadOnly<TransformComponent>(entity).Position;
  }

  private static FP64 GetHealth(Frame frame, int unitId) {
    TryGetEntityByUnitId(frame, unitId, out var entity).Should().BeTrue();
    frame.Has<Health>(entity).Should().BeTrue();
    return frame.GetReadOnly<Health>(entity).Current;
  }

  private static FP64 GetAttackDamage(Frame frame, int unitId) {
    TryGetEntityByUnitId(frame, unitId, out var entity).Should().BeTrue();
    frame.Has<StatsComponent>(entity).Should().BeTrue();
    return frame.GetReadOnly<StatsComponent>(entity).AttackDamage;
  }

  private static FP64 GetArmor(Frame frame, int unitId) {
    TryGetEntityByUnitId(frame, unitId, out var entity).Should().BeTrue();
    frame.Has<StatsComponent>(entity).Should().BeTrue();
    return frame.GetReadOnly<StatsComponent>(entity).Armor;
  }

  private static int GetCooldown(Frame frame, int unitId) {
    TryGetEntityByUnitId(frame, unitId, out var entity).Should().BeTrue();
    frame.Has<Combat>(entity).Should().BeTrue();
    return frame.GetReadOnly<Combat>(entity).CooldownRemainingTicks;
  }

  private static void KillUnit(SimHarness harness, int unitId) {
    TryGetEntityByUnitId(harness.Frame, unitId, out var entity).Should().BeTrue();
    harness.Frame.Has<Health>(entity).Should().BeTrue("only units with Health can be killed in this helper");
    ref var health = ref harness.Frame.Get<Health>(entity);
    health.Current = 0;
  }

  private static (UnitSnapshot Source, UnitSnapshot Target) SpawnFirstWave(SimHarness harness) {
    var rules = harness.AssetRegistry.Get<Meesles.Avalon.Sim.Assets.WaveRulesAsset>();
    for (int tick = 0; tick <= rules.FirstWaveDelayTicks; tick++)
      harness.Tick();

    var minions = GetMinions(harness);
    return (
      minions.Single(minion => minion.TeamId == 1),
      minions.Single(minion => minion.TeamId == 2));
  }

  private static UnitSnapshot[] GetMinions(SimHarness harness) {
    var frame = harness.Frame;
    var minions = new List<UnitSnapshot>();
    var filter = frame.Filter<Minion, UnitIdComponent, TeamComponent, TransformComponent>();
    while (filter.Next(out var entity)) {
      ref readonly var unit = ref frame.GetReadOnly<UnitIdComponent>(entity);
      ref readonly var team = ref frame.GetReadOnly<TeamComponent>(entity);
      ref readonly var transform = ref frame.GetReadOnly<TransformComponent>(entity);
      minions.Add(new UnitSnapshot(unit.UnitId, team.TeamId, transform.Position));
    }

    return minions.OrderBy(minion => minion.UnitId).ToArray();
  }

  private static UnitSnapshot[] GetTurrets(SimHarness harness) {
    var frame = harness.Frame;
    var turrets = new List<UnitSnapshot>();
    var filter = frame.Filter<Turret, UnitIdComponent, TeamComponent, TransformComponent>();
    while (filter.Next(out var entity)) {
      ref readonly var unit = ref frame.GetReadOnly<UnitIdComponent>(entity);
      ref readonly var team = ref frame.GetReadOnly<TeamComponent>(entity);
      ref readonly var transform = ref frame.GetReadOnly<TransformComponent>(entity);
      turrets.Add(new UnitSnapshot(unit.UnitId, team.TeamId, transform.Position));
    }

    return turrets.OrderBy(turret => turret.UnitId).ToArray();
  }

  private static UnitSnapshot[] GetCrystals(SimHarness harness) {
    var frame = harness.Frame;
    var crystals = new List<UnitSnapshot>();
    var filter = frame.Filter<Crystal, UnitIdComponent, TeamComponent, TransformComponent>();
    while (filter.Next(out var entity)) {
      ref readonly var unit = ref frame.GetReadOnly<UnitIdComponent>(entity);
      ref readonly var team = ref frame.GetReadOnly<TeamComponent>(entity);
      ref readonly var transform = ref frame.GetReadOnly<TransformComponent>(entity);
      crystals.Add(new UnitSnapshot(unit.UnitId, team.TeamId, transform.Position));
    }

    return crystals.OrderBy(crystal => crystal.UnitId).ToArray();
  }

  // Pushes every unit hostile to teamId far outside any acquisition radius so a test can place the
  // handful it cares about and know nothing else is a candidate.
  private static void ScatterHostiles(SimHarness harness, int teamId) {
    var frame = harness.Frame;
    var hostiles = new List<EntityRef>();
    var filter = frame.Filter<UnitIdComponent, TeamComponent, TransformComponent>();
    while (filter.Next(out var entity)) {
      if (frame.GetReadOnly<TeamComponent>(entity).TeamId != teamId)
        hostiles.Add(entity);
    }

    for (int i = 0; i < hostiles.Count; i++) {
      ref var transform = ref frame.Get<TransformComponent>(hostiles[i]);
      transform.Position = new FPVector3(FP64.FromInt(200 + i * 20), FP64.Zero, FP64.FromInt(200));
    }
  }

  private static FPVector3 SnapToWalkable(SimHarness harness, FPVector3 target) {
    return Meesles.Avalon.Sim.Navigation.NavTargets.SnapToWalkable(harness.Navigation.Query, target);
  }

  // These setups line units up a few metres apart and assert on what each can reach, so the line has
  // to sit on ground the nav agents won't be snapped off. The map origin is not that: the fountain
  // holes the navmesh out to ~4m there, and a minion placed at (0,0) lands on the rim metres from
  // where the test put it. z=-14 is the map's longest clear corridor (x -21.5..25).
  private static readonly FPVector3 OpenGround = new(FP64.FromInt(-18), FP64.Zero, FP64.FromInt(-14));

  private static FPVector3 EastOfOpenGround(FP64 offset) {
    return new FPVector3(OpenGround.x + offset, FP64.Zero, OpenGround.z);
  }

  private static void SetPosition(SimHarness harness, int unitId, FPVector3 position) {
    var frame = harness.Frame;
    TryGetEntityByUnitId(frame, unitId, out var entity).Should().BeTrue();
    frame.Has<TransformComponent>(entity).Should().BeTrue();
    ref var transform = ref frame.Get<TransformComponent>(entity);
    transform.Position = position;
  }

  private static void SetMoveTarget(SimHarness harness, int unitId, FPVector3 target) {
    var frame = harness.Frame;
    TryGetEntityByUnitId(frame, unitId, out var entity).Should().BeTrue();
    if (frame.Has<UnitMoveTarget>(entity)) {
      ref var moveTarget = ref frame.Get<UnitMoveTarget>(entity);
      moveTarget.Target = target;
      return;
    }

    frame.Add(entity, new UnitMoveTarget { Target = target });
  }

  private static void SetHealth(SimHarness harness, int unitId, int current) {
    var frame = harness.Frame;
    TryGetEntityByUnitId(frame, unitId, out var entity).Should().BeTrue();
    frame.Has<Health>(entity).Should().BeTrue();
    ref var health = ref frame.Get<Health>(entity);
    health.Current = current;
  }

  private static void SetArmor(SimHarness harness, int unitId, int armor) {
    var frame = harness.Frame;
    TryGetEntityByUnitId(frame, unitId, out var entity).Should().BeTrue();
    frame.Has<StatsComponent>(entity).Should().BeTrue();
    frame.Get<StatsComponent>(entity).Set(StatType.Armor, FP64.FromInt(armor));
  }

  private static void ClearAttackTargets(SimHarness harness) {
    var frame = harness.Frame;
    var entities = new List<EntityRef>();
    var filter = frame.Filter<AttackTargetUnitId>();
    while (filter.Next(out var entity))
      entities.Add(entity);

    foreach (var entity in entities) {
      frame.Remove<AttackTargetUnitId>(entity);
      if (!frame.Has<Combat>(entity))
        continue;

      ref var combat = ref frame.Get<Combat>(entity);
      combat.TargetUnitId = 0;
    }
  }

  private static int SpawnTestMinion(SimHarness harness, int teamId, FPVector3 position) {
    var frame = harness.Frame;
    var entity = frame.CreateEntity();
    int unitId = UnitLookup.NextUnitId(ref frame);

    frame.Add(entity, TransformFactory.At(position));
    frame.Add(entity, new UnitIdComponent {
      UnitId = unitId,
      UnitTypeId = SimulationSetup.MinionUnitTypeId,
    });
    frame.Add(entity, new TeamComponent { TeamId = teamId });
    frame.Add(entity, new Minion { WaveId = 99 });
    frame.Add(entity, new Controllable());
    frame.Add(entity, new Health(FP64.FromInt(100)));
    frame.Add(entity, StatsComponent.Create()
      .With(StatType.MaxHealth, FP64.FromInt(100))
      .With(StatType.AttackDamage, FP64.FromInt(10))
      .With(StatType.AttackRange, FP64.FromInt(2))
      .With(StatType.AcquisitionRange, FP64.FromInt(6)));
    frame.Add(entity, new Combat());

    return unitId;
  }

  private static bool TryGetEntityByUnitId(Frame frame, int unitId, out EntityRef entity) {
    var filter = frame.Filter<UnitIdComponent>();
    while (filter.Next(out entity)) {
      ref readonly var unit = ref frame.GetReadOnly<UnitIdComponent>(entity);
      if (unit.UnitId == unitId)
        return true;
    }

    entity = default;
    return false;
  }

  private record UnitSnapshot(int UnitId, int TeamId, FPVector3 Position);
}
