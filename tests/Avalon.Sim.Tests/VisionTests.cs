using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Commands;
using Meesles.Avalon.Sim.Components;
using Meesles.Avalon.Sim.Factories;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using Xunit;

namespace Meesles.Avalon.Sim.Tests;

public class VisionTests {
  [Theory]
  [InlineData(-6, 8, -6, 2, true)]
  [InlineData(-6, -8, -6, -14, true)]
  [InlineData(0, 8, 0, -8, false)]
  public void WorldClearingHasSightAcrossOpenGroundButNotThroughFountain(
    int ax, int az, int bx, int bz, bool visible) {
    var map = SimHarness.CreateInitialized().AssetRegistry.Get<MapLayoutAsset>();
    Visibility.CanSee(map, Point(ax, az), Point(bx, bz), FP64.FromInt(18), FP64.Zero)
      .Should().Be(visible);
  }

  [Fact]
  public void NoFogRevealsOnlyTheIssuingTeamAndRestoresSightWhenDisabled() {
    var frame = CreateFrame();
    var hero = Spawn(ref frame, 1, 0, 0);
    frame.Add(hero, new Hero(7, AssetIds.HeroHairyWizard));
    var target = Spawn(ref frame, 2, 4, 0);
    var map = frame.AssetRegistry.Get<MapLayoutAsset>();
    map.VisionOrigin = Point(-10, -10);
    map.VisionSize = Point(20, 20);
    var raster = new VisionRaster();
    var pixels = new byte[400];

    foreach (var enabled in new[] { true, false }) {
      new CommandSystem().OnCommand(ref frame, SimHarness.SetCheatCommand(7, frame.Tick, CheatFlags.NoFog, enabled));
      new VisionSystem().Update(ref frame);
      Visibility.IsUnitVisible(ref frame, 1, target).Should().Be(enabled);
      Visibility.IsPositionVisible(ref frame, 1, Point(40, 40)).Should().Be(enabled);
      Visibility.IsPositionVisible(ref frame, 2, Point(40, 40)).Should().BeFalse();
      frame.GetReadOnly<UnitVisibility>(target).Teams.Should().Be(enabled ? 3UL : 2UL);
      Visibility.Rasterize(ref frame, 1, map.VisionOrigin, map.VisionSize, 20, 20, pixels);
      pixels[10 * 20 + 14].Should().Be(enabled ? (byte)255 : (byte)0);
      raster.Capture(ref frame, 1, map);
      var captured = new byte[400];
      raster.Render(20, 20, captured);
      captured.Should().Equal(pixels);
    }
  }

  [Fact]
  public void NoFogPredictionRollsBack() {
    var rollback = RollbackHarness.Create();
    rollback.Advance(3);
    rollback.MispredictAndRollback(4, tick => [SimHarness.SetCheatCommand(1, tick, CheatFlags.NoFog)]);
    rollback.AdvanceAndCompare(8).Should().BeEmpty();
    var frame = rollback.Client.Frame;
    CheatsController.IsFogDisabled(ref frame, 1).Should().BeFalse();
  }

  [Fact]
  public void TeamVisionUnionsLivingSourcesAndRebuildsAfterDeath() {
    var frame = CreateFrame();
    Spawn(ref frame, 1, 0, 0);
    var target = Spawn(ref frame, 2, 4, 0);
    Visibility.IsUnitVisible(ref frame, 1, target).Should().BeFalse();
    var ally = Spawn(ref frame, 1, 8, 0);
    new VisionSystem().Update(ref frame);
    frame.GetReadOnly<UnitVisibility>(target).Teams.Should().Be(3);
    frame.Get<Health>(ally).Current = FP64.Zero;
    new VisionSystem().Update(ref frame);
    frame.GetReadOnly<UnitVisibility>(target).Teams.Should().Be(2);
    Visibility.IsUnitVisible(ref frame, 1, target).Should().BeFalse();
  }

  [Fact]
  public void NewAttackOrdersRequireSightButExistingOrdersSurviveLosingIt() {
    var frame = CreateFrame();
    var attacker = Spawn(ref frame, 1, 0, 0);
    frame.Add(attacker, new Hero(1, AssetIds.HeroHairyWizard));
    frame.Add(attacker, new Controllable());
    frame.Add(attacker, new Combat());
    var target = Spawn(ref frame, 2, 4, 0);
    var command = new AttackCommand {
      PlayerId = 1,
      TargetUnitId = frame.GetReadOnly<UnitIdentity>(target).UnitId
    };
    command.UnitIds.Add(frame.GetReadOnly<UnitIdentity>(attacker).UnitId);
    var commands = new CommandSystem();
    commands.OnCommand(ref frame, command);
    frame.Has<AttackTargetUnitId>(attacker).Should().BeFalse();

    frame.Get<TransformComponent>(target).Position = new FPVector3(FP64.One, FP64.Zero, FP64.Zero);
    commands.OnCommand(ref frame, command);
    frame.Has<AttackTargetUnitId>(attacker).Should().BeTrue();
    frame.Get<TransformComponent>(target).Position = new FPVector3(FP64.FromInt(30), FP64.Zero, FP64.Zero);
    new VisionSystem().Update(ref frame);
    new AttackIntentSystem().Update(ref frame);
    Visibility.IsUnitVisible(ref frame, 1, target).Should().BeFalse();
    frame.GetReadOnly<AttackTargetUnitId>(attacker).TargetUnitId.Should().Be(command.TargetUnitId);
  }

  [Fact]
  public void AutomaticAcquisitionRejectsAnOccludedEnemy() {
    var frame = CreateFrame();
    var attacker = Spawn(ref frame, 1, 0, 0);
    frame.Add(attacker, new Combat());
    Spawn(ref frame, 2, 4, 0);
    new TargetAcquisitionSystem().Update(ref frame);
    frame.Has<AttackTargetUnitId>(attacker).Should().BeFalse();
  }

  [Fact]
  public void TextureUsesTheSameSightAndRangeRulesAtPixelCenters() {
    var frame = CreateFrame();
    Spawn(ref frame, 1, 0, 0);
    var pixels = new byte[400];
    var origin = Point(-10, -10);
    var size = Point(20, 20);
    Visibility.Rasterize(ref frame, 1, origin, size, 20, 20, pixels);
    for (var z = 0; z < 20; z++)
      for (var x = 0; x < 20; x++) {
        var point = origin + new FPVector2(FP64.FromInt(x) + FP64.One / FP64.FromInt(2),
          FP64.FromInt(z) + FP64.One / FP64.FromInt(2));
        (pixels[z * 20 + x] != 0).Should().Be(Visibility.IsPositionVisible(ref frame, 1, point));
      }
    pixels[10 * 20 + 14].Should().Be(0); // Behind the wall.
    pixels[10 * 20 + 10].Should().Be(255);
  }

  [Fact]
  public async Task CapturedRasterCanRenderAfterTheLiveFrameChanges() {
    var frame = CreateFrame();
    var source = Spawn(ref frame, 1, 0, 0);
    var map = frame.AssetRegistry.Get<MapLayoutAsset>();
    map.VisionOrigin = Point(-10, -10);
    map.VisionSize = Point(20, 20);
    var expected = new byte[400];
    Visibility.Rasterize(ref frame, 1, map.VisionOrigin, map.VisionSize, 20, 20, expected);
    var raster = new VisionRaster();
    raster.Capture(ref frame, 1, map);
    frame.Get<TransformComponent>(source).Position = new FPVector3(FP64.FromInt(40), FP64.Zero, FP64.Zero);
    frame.DestroyEntity(source);
    var actual = new byte[400];
    await Task.Run(() => raster.Render(20, 20, actual));
    actual.Should().Equal(expected);
    raster.Capture(ref frame, 1, map);
    raster.Render(20, 20, actual);
    actual.Should().OnlyContain(pixel => pixel == 0);
  }

  [Theory]
  [InlineData(0, 0, 8, 8)]
  [InlineData(8, 8, 0, 0)]
  [InlineData(-8, -8, -4, -4)]
  [InlineData(0, 4, 4, 4)]
  public void GridBoundaryRaysTerminateAndRemainSymmetric(int ax, int az, int bx, int bz) {
    var geometry = new SightGeometry(Rectangle(2, -5, 3, 5), FP64.FromInt(4));
    geometry.HasLineOfSight(Point(ax, az), Point(bx, bz), FP64.Zero, FP64.Zero)
      .Should().Be(geometry.HasLineOfSight(Point(bx, bz), Point(ax, az), FP64.Zero, FP64.Zero));
  }

  [Fact]
  public void ASourceInsideItsOwnStructureCanSeeOutButNotThroughAnotherObstacle() {
    var edges = new List<FPVector2>(Rectangle(-50, -50, 50, 50));
    edges.AddRange(Rectangle(-1, -1, 1, 1));
    edges.AddRange(Rectangle(3, -2, 4, 2));
    var geometry = new SightGeometry(edges.ToArray(), FP64.FromInt(4));
    geometry.IsWalkable(Point(0, 0)).Should().BeFalse();
    geometry.IsWalkable(Point(2, 0)).Should().BeTrue();
    geometry.HasLineOfSight(Point(0, 0), Point(2, 0), FP64.Zero, FP64.Zero, true).Should().BeTrue();
    geometry.HasLineOfSight(Point(0, 0), Point(6, 0), FP64.Zero, FP64.Zero, true).Should().BeFalse();
  }

  [Fact]
  public void PredictionRollbackRestoresVisibilityAndRasterOutput() {
    var rollback = RollbackHarness.Create();
    rollback.Advance(3);
    rollback.MispredictAndRollback(4, _ => [], harness => {
      var frame = harness.Frame;
      frame.Get<TransformComponent>(harness.FindHero(1)).Position =
        new FPVector3(FP64.FromInt(20), FP64.Zero, FP64.FromInt(20));
    });
    rollback.AdvanceAndCompare(8).Should().BeEmpty();
    var server = rollback.Server.Frame;
    var client = rollback.Client.Frame;
    var a = new byte[1024];
    var b = new byte[1024];
    Visibility.Rasterize(ref server, 1, Point(-50, -50), Point(100, 100), 32, 32, a);
    Visibility.Rasterize(ref client, 1, Point(-50, -50), Point(100, 100), 32, 32, b);
    a.Should().Equal(b);
  }

  private static Frame CreateFrame() {
    WarmupRegistry.RunAll();
    var edges = new List<FPVector2>(Rectangle(-50, -50, 50, 50));
    edges.AddRange(Rectangle(2, -5, 3, 5));
    IDataAssetRegistryBuilder builder = new DataAssetRegistry();
    builder.RegisterRange(new IDataAsset[] {
      new VisionRulesAsset(AssetIds.VisionRules),
      new CombatRulesAsset(AssetIds.CombatRules) { TargetGridCellSize = FP64.FromInt(10) },
      new MapLayoutAsset(AssetIds.MapLayout) { SightEdges = edges.ToArray(), VisionCellSize = FP64.FromInt(4) }
    });
    var simulation = new EcsSimulation(SimHarness.DefaultMaxEntities, 8, 33, assetRegistry: builder.Build());
    simulation.Initialize();
    var frame = simulation.Frame;
    UnitLookup.InitializeUnitIds(ref frame);
    return frame;
  }

  private static EntityRef Spawn(ref Frame frame, int teamId, int x, int z) {
    var entity = frame.CreateEntity();
    frame.Add(entity, TransformFactory.At(new FPVector3(FP64.FromInt(x), FP64.Zero, FP64.FromInt(z))));
    frame.Add(entity, new Team(teamId));
    frame.Add(entity, new UnitIdentity { UnitId = UnitLookup.NextUnitId(ref frame), UnitTypeId = SimulationSetup.MinionUnitTypeId });
    frame.Add(entity, new Minion());
    frame.Add(entity, new Health(FP64.FromInt(100)));
    frame.Add(entity, Stats.Create().With(StatType.AcquisitionRange, FP64.FromInt(10)));
    return entity;
  }

  private static FPVector2 Point(int x, int z) => new(FP64.FromInt(x), FP64.FromInt(z));

  private static FPVector2[] Rectangle(int minX, int minZ, int maxX, int maxZ) => [
    Point(minX, minZ), Point(maxX, minZ), Point(maxX, minZ), Point(maxX, maxZ),
    Point(maxX, maxZ), Point(minX, maxZ), Point(minX, maxZ), Point(minX, minZ)
  ];
}
