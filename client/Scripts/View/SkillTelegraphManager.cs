using System;
using System.Collections.Generic;
using Godot;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using Meesles.Avalon.Sim.Heroes;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Godot;

namespace Meesles.Avalon.Client.Scripts.View;

// Spawns the ground telegraph for a cast. Lifecycle mirrors VfxManager: the owning GameNode calls
// Attach when a session starts and Detach when it stops.
//
// Attach builds a TelegraphLayer under the game node and parents every telegraph to it, so cast FX
// live with the session rather than with the environment scene. The layer also hosts the addon's
// ConTelegraphManager, which packs every visible telegraph into one data texture each frame for the
// ground material's next_pass to read. A telegraph node therefore has no mesh of its own, and the
// packer finds it through a tree group, so the layer is about ownership, not rendering.
//
// **A map's ground material must carry con_telegraph_next_pass.tres as its next_pass or nothing here
// draws** - the packer writes the data texture either way, and there is no error to see. World.tscn
// and NavPlayground.tscn have it; a new map needs it too.
//
// SkillCastEvent is Synced, so this rides the confirmed stream. On a predicting client that costs the
// telegraph a round trip; making it instant means flipping the event to Regular and adding
// OnPredicted/OnCanceled here, which is a sim-side change and not one this needed yet.
//
// It also draws the persistent trail telegraphs (Snail Trail): one circle per laid segment, its fill
// sweeping in over the segment's lifetime the way a charged burst's ring closes over its wind-up, so
// a full circle reads as "about to expire". SkillTrailSegmentSpawnedEvent is Regular, so those ride
// OnFx (+ OnCanceled to drop a mispredicted segment), keyed on SegmentId against a resim re-raise.
public class SkillTelegraphManager {
  private const string TelegraphScenePath = "res://Scenes/FX/Telegraphs/SkillTelegraph.tscn";
  private const string PackerScriptPath = "res://addons/constructive_telegraphs/src/con_telegraph_manager.gd";

  private static PackedScene _telegraphScene;
  private static readonly Dictionary<string, Resource> FamilyCache = new();

  private readonly TelegraphCatalog _catalog = TelegraphCatalog.CreateDefault();

  private readonly Dictionary<int, Node3D> _trailSegments = new();

  private Node3D _aimNode;
  private int _aimSlot = -1;
  private IKlothoEngine _engine;
  private Node3D _layer;
  private Node _packer;
  private IDisposable _skillCastSub;
  private IDisposable _trailSpawnSub;
  private IDisposable _trailCanceledSub;
  private EntityViewUpdaterNode _view;

  // Registry and local player id come off the engine per event rather than being captured here:
  // MultiplayerGameNode adopts an already-running session from the lobby, and its own registry field
  // is only populated on the direct-join fallback path.
  public void Attach(SimEventHub events, EntityViewUpdaterNode view, IKlothoEngine engine, Node layerParent) {
    Detach();
    _view = view;
    _engine = engine;
    CreateLayer(layerParent);
    _skillCastSub = events.OnConfirmed<SkillCastEvent>(HandleSkillCast);
    _trailSpawnSub = events.OnFx<SkillTrailSegmentSpawnedEvent>(HandleTrailSegmentSpawned);
    _trailCanceledSub = events.OnCanceled<SkillTrailSegmentSpawnedEvent>(HandleTrailSegmentCanceled);
  }

  public void Detach() {
    HideAim();
    _skillCastSub?.Dispose();
    _skillCastSub = null;
    _trailSpawnSub?.Dispose();
    _trailSpawnSub = null;
    _trailCanceledSub?.Dispose();
    _trailCanceledSub = null;
    _trailSegments.Clear(); // the _layer.QueueFree() below frees the telegraph nodes themselves
    ClearGroundOverlay();
    if (_layer != null && GodotObject.IsInstanceValid(_layer))
      _layer.QueueFree();
    _layer = null;
    _packer = null;
    _view = null;
    _engine = null;
  }

  private void CreateLayer(Node layerParent) {
    if (layerParent == null) return;
    _layer = new Node3D { Name = "TelegraphLayer" };
    layerParent.AddChild(_layer);

    var script = GD.Load<GDScript>(PackerScriptPath);
    _packer = script?.New().As<Node>();
    if (_packer == null) {
      GD.PushError($"[Telegraph] {PackerScriptPath} failed to load — telegraphs will not draw.");
      return;
    }

    _packer.Name = "ConTelegraphPacker";
    _layer.AddChild(_packer);
  }

  // The packer writes into a shared material the world scene keeps using, so the last frame's decals
  // would stay burned into the ground once it stops running. Push one empty frame on the way out.
  private void ClearGroundOverlay() {
    if (_packer == null || !GodotObject.IsInstanceValid(_packer)) return;
    _packer.Call("init_data_transfer_texture");
    _packer.Call("update_data_transfer_texture");
    _packer.Call("update_surface_overlay_material");
  }

  // Aim preview for a held skill key. Same shape the cast draws, parked at zero fill so it reads as an
  // outline rather than a sweep, re-aimed every frame while the key is down. Called unconditionally by
  // InputCapture: a slot with no catalog row, or one that resolves to nothing this frame, just hides.
  public void ShowAim(int slot, Vector3 aimPoint) {
    if (_view == null || _engine == null || _layer == null) {
      HideAim();
      return;
    }

    var frame = _engine.PredictedFrame.Frame;
    var aim = new FPVector3(FP64.FromFloat(aimPoint.X), FP64.Zero, FP64.FromFloat(aimPoint.Z));
    if (frame == null || !TryResolveAim(ref frame, slot, aim, out var skill, out var def,
          out var casterPosition, out var facing, out var casterUnitId)) {
      HideAim();
      return;
    }

    if (_aimSlot != slot)
      HideAim();

    if (_aimNode == null) {
      _aimNode = SpawnTelegraph(skill, def, def.OwnFamilyPath);
      if (_aimNode == null) return;
      _aimSlot = slot;
    }

    _view.ViewsByUnitId.TryGetValue(casterUnitId, out var casterView);
    var origin = casterView?.GlobalPosition ?? casterPosition.ToVector3();
    origin.Y = casterPosition.y.ToFloat();

    _aimNode.GlobalPosition = origin;
    _aimNode.LookAt(origin + facing.ToVector3(), Vector3.Up);
  }

  public void HideAim() {
    if (_aimNode != null && GodotObject.IsInstanceValid(_aimNode))
      _aimNode.QueueFree();
    _aimNode = null;
    _aimSlot = -1;
  }

  // Runs the cursor point through the sim's own clamp and facing fallback, so the preview stands where
  // the cast will actually land rather than where the mouse is. Stays in fixed point: the caller does
  // the conversion to render space.
  private bool TryResolveAim(ref Frame frame, int slot, FPVector3 aim, out SkillAsset skill,
    out TelegraphCatalog.TelegraphDef def, out FPVector3 casterPosition, out FPVector3 facing,
    out int casterUnitId) {
    skill = null;
    def = default;
    casterPosition = FPVector3.Zero;
    facing = FPVector3.Zero;
    casterUnitId = 0;

    if (!UnitLookup.TryGetPlayerHero(ref frame, _engine.LocalPlayerId, out var hero)) return false;
    if (!frame.Has<Skills>(hero)) return false;

    var skillAssetId = frame.GetReadOnly<Skills>(hero).GetSkillAssetId(slot);
    if (!_catalog.TryResolve(skillAssetId, out def)) return false;
    if (!frame.AssetRegistry.TryGet<SkillAsset>(skillAssetId, out skill)) return false;

    casterPosition = frame.Has<TransformComponent>(hero)
      ? frame.GetReadOnly<TransformComponent>(hero).Position
      : FPVector3.Zero;
    var target = SkillAim.ClampToCastRange(ref frame, hero, skill, casterPosition, aim);
    facing = SkillAim.Direction(ref frame, hero, casterPosition, target);
    if (facing.sqrMagnitude <= FP64.Zero) return false;

    casterUnitId = UnitLookup.GetUnitId(ref frame, hero);
    return true;
  }

  private void HandleSkillCast(SkillCastEvent evt) {
    if (_view == null || _layer == null) return;
    if (!_catalog.TryResolve(evt.SkillAssetId, out var def)) return;

    var registry = _engine?.PredictedFrame.Frame?.AssetRegistry;
    if (registry == null || !registry.TryGet<SkillAsset>(evt.SkillAssetId, out var skill)) return;

    _view.ViewsByUnitId.TryGetValue(evt.UnitId, out var casterView);
    if (!TryResolveCastAim(evt, casterView, out var origin, out var direction)) return;

    var own = evt.PlayerId == _engine.LocalPlayerId;
    var telegraph = SpawnTelegraph(skill, def, own ? def.OwnFamilyPath : def.HostileFamilyPath);
    if (telegraph == null) return;

    // The caster's own preview is still up under the finished cast; drop it so only the sweep shows.
    if (own) HideAim();

    telegraph.GlobalPosition = origin;
    telegraph.LookAt(origin + direction, Vector3.Up); // -Z is the lanes' forward, matching FillMode.FORWARD
    telegraph.Call("play");
  }

  // One circle per laid segment. configure_circle sets the instance's fill_duration to the segment
  // lifetime and play() sweeps the fill in over it, then the scene fades out and frees itself off
  // fade_out_completed - there is no despawn event to wait on. The event is pooled, so read its
  // fields into locals before the TreeExiting closure captures anything.
  private void HandleTrailSegmentSpawned(SkillTrailSegmentSpawnedEvent evt) {
    var segmentId = evt.SegmentId;
    if (_layer == null || _engine == null || _trailSegments.ContainsKey(segmentId)) return;
    if (!_catalog.TryResolve(evt.SkillAssetId, out var def)) return;

    var frame = _engine.PredictedFrame.Frame;
    var own = frame != null
              && UnitLookup.TryGetPlayerTeamId(ref frame, _engine.LocalPlayerId, out var localTeam)
              && evt.TeamId == localTeam;

    var family = LoadFamily(own ? def.OwnFamilyPath : def.HostileFamilyPath);
    if (family == null) return;

    _telegraphScene ??= GD.Load<PackedScene>(TelegraphScenePath);
    if (_telegraphScene?.Instantiate() is not Node3D telegraph) return;

    _layer.AddChild(telegraph);
    var lifetimeSeconds = evt.LifetimeTicks * _engine.TickInterval / 1000f;
    telegraph.Call("configure_circle", family, evt.Width.ToFloat(), lifetimeSeconds, def.Height);
    telegraph.GlobalPosition = evt.Position.ToVector3();
    telegraph.Call("play");

    _trailSegments[segmentId] = telegraph;
    telegraph.TreeExiting += () => _trailSegments.Remove(segmentId);
  }

  private void HandleTrailSegmentCanceled(SkillTrailSegmentSpawnedEvent evt) {
    if (_trailSegments.Remove(evt.SegmentId, out var telegraph) && GodotObject.IsInstanceValid(telegraph))
      telegraph.QueueFree();
  }

  private Node3D SpawnTelegraph(SkillAsset skill, TelegraphCatalog.TelegraphDef def, string familyPath) {
    var family = LoadFamily(familyPath);
    if (family == null) return null;

    _telegraphScene ??= GD.Load<PackedScene>(TelegraphScenePath);
    if (_telegraphScene?.Instantiate() is not Node3D telegraph) return null;

    _layer.AddChild(telegraph);
    if (Configure(telegraph, skill, def, family)) return telegraph;

    telegraph.QueueFree();
    return null;
  }

  // One shape per row: a cone row carries no projectile block, a projectile row no cone, an area row
  // neither, so the row itself picks which configure runs. A row that authored none draws nothing.
  private static bool Configure(Node3D telegraph, SkillAsset skill, TelegraphCatalog.TelegraphDef def,
    Resource family) {
    if (skill.HasArea) {
      // A charged burst fills over its own wind-up, so the ring closes exactly as the sim detonates.
      var fillSeconds = skill.ChargeDurationMs > 0 ? skill.ChargeDurationMs / 1000f : def.FillSeconds;
      telegraph.Call("configure_circle", family, skill.AreaRadius.ToFloat(), fillSeconds, def.Height);
      return true;
    }

    if (skill.HasCone) {
      telegraph.Call("configure_cone",
        family,
        skill.ConeRange.ToFloat(),
        skill.ConeAngleDegrees.ToFloat(),
        def.FillSeconds,
        def.Height);
      return true;
    }

    var range = skill.ProjectileRange.ToFloat();
    var speed = skill.ProjectileSpeed.ToFloat();
    if (skill.ProjectileCount <= 0 || range <= 0f || speed <= 0f) return false;

    telegraph.Call("configure",
      family,
      skill.ProjectileCount,
      skill.ProjectileSpacing.ToFloat(),
      skill.ProjectileRadius.ToFloat(),
      range,
      skill.ProjectileSpawnOffset.ToFloat(),
      range / speed, // bars sweep at the speed the bullets travel
      def.Height);
    return true;
  }

  // Origin follows the rendered caster when it has a view, so the lanes start at the hero the player is
  // looking at rather than at the sim position the interpolated view trails.
  private static bool TryResolveCastAim(SkillCastEvent evt, Node3D casterView, out Vector3 origin,
    out Vector3 direction) {
    var castPosition = evt.Position.ToVector3();
    origin = casterView?.GlobalPosition ?? castPosition;
    origin.Y = castPosition.Y;

    direction = evt.TargetPosition.ToVector3() - castPosition;
    direction.Y = 0f;

    // Aim landed on the caster: the sim falls back to the caster's facing, so do the same.
    if (direction.LengthSquared() <= 0.0001f) {
      if (casterView == null) return false;
      direction = -casterView.GlobalBasis.Z;
      direction.Y = 0f;
      if (direction.LengthSquared() <= 0.0001f) return false;
    }

    direction = direction.Normalized();
    return true;
  }

  private static Resource LoadFamily(string path) {
    if (FamilyCache.TryGetValue(path, out var cached)) return cached;
    var family = GD.Load<Resource>(path);
    FamilyCache[path] = family;
    return family;
  }
}
