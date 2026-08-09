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
// The decal itself is drawn by the constructive_telegraphs addon, which is not a UI layer: the
// ConTelegraphManager node in World.tscn packs every visible telegraph into one data texture each
// frame, and the ground material's next_pass reads it. So a telegraph node has no mesh of its own and
// its parent is irrelevant — it only has to be in the tree and in the addon's group.
//
// SkillCastEvent is Synced, so this rides the confirmed stream. On a predicting client that costs the
// telegraph a round trip; making it instant means flipping the event to Regular and adding
// OnPredicted/OnCanceled here, which is a sim-side change and not one this needed yet.
public class SkillTelegraphManager {
  private const string TelegraphScenePath = "res://Scenes/FX/Telegraphs/SkillTelegraph.tscn";

  private static PackedScene _telegraphScene;
  private static readonly Dictionary<string, Resource> FamilyCache = new();

  private readonly TelegraphCatalog _catalog = TelegraphCatalog.CreateDefault();

  private Node3D _aimNode;
  private int _aimSlot = -1;
  private IKlothoEngine _engine;
  private IDisposable _skillCastSub;
  private EntityViewUpdaterNode _view;

  // Registry and local player id come off the engine per event rather than being captured here:
  // MultiplayerGameNode adopts an already-running session from the lobby, and its own registry field
  // is only populated on the direct-join fallback path.
  public void Attach(SimEventHub events, EntityViewUpdaterNode view, IKlothoEngine engine) {
    Detach();
    _view = view;
    _engine = engine;
    _skillCastSub = events.OnConfirmed<SkillCastEvent>(HandleSkillCast);
  }

  public void Detach() {
    HideAim();
    _skillCastSub?.Dispose();
    _skillCastSub = null;
    _view = null;
    _engine = null;
  }

  // Aim preview for a held skill key. Same lanes the cast draws, parked at zero fill so it reads as an
  // outline rather than a sweep, re-aimed every frame while the key is down. Called unconditionally by
  // InputCapture: a slot with no catalog row, or one that resolves to nothing this frame, just hides.
  public void ShowAim(int slot, Vector3 aimPoint) {
    if (_view == null || _engine == null) {
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
    if (!frame.Has<SkillsComponent>(hero)) return false;

    var skillAssetId = frame.GetReadOnly<SkillsComponent>(hero).GetSkillAssetId(slot);
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
    if (_view == null) return;
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

  private Node3D SpawnTelegraph(SkillAsset skill, TelegraphCatalog.TelegraphDef def, string familyPath) {
    var range = skill.ProjectileRange.ToFloat();
    var speed = skill.ProjectileSpeed.ToFloat();
    if (skill.ProjectileCount <= 0 || range <= 0f || speed <= 0f) return null;

    var family = LoadFamily(familyPath);
    if (family == null) return null;

    _telegraphScene ??= GD.Load<PackedScene>(TelegraphScenePath);
    if (_telegraphScene?.Instantiate() is not Node3D telegraph) return null;

    _view.AddChild(telegraph);
    telegraph.Call("configure",
      family,
      skill.ProjectileCount,
      skill.ProjectileSpacing.ToFloat(),
      skill.ProjectileRadius.ToFloat(),
      range,
      skill.ProjectileSpawnOffset.ToFloat(),
      range / speed, // bars sweep at the speed the bullets travel
      def.LaneHeight);
    return telegraph;
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
