using System;
using System.Collections.Generic;
using Godot;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Assets;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
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
    _skillCastSub?.Dispose();
    _skillCastSub = null;
    _view = null;
    _engine = null;
  }

  private void HandleSkillCast(SkillCastEvent evt) {
    if (_view == null) return;
    if (!_catalog.TryResolve(evt.SkillAssetId, out var def)) return;

    var registry = _engine?.PredictedFrame.Frame?.AssetRegistry;
    if (registry == null || !registry.TryGet<SkillAsset>(evt.SkillAssetId, out var skill)) return;

    var range = skill.ProjectileRange.ToFloat();
    var speed = skill.ProjectileSpeed.ToFloat();
    if (skill.ProjectileCount <= 0 || range <= 0f || speed <= 0f) return;

    _view.ViewsByUnitId.TryGetValue(evt.UnitId, out var casterView);
    if (!TryResolveAim(evt, casterView, out var origin, out var direction)) return;

    var family = LoadFamily(evt.PlayerId == _engine.LocalPlayerId ? def.OwnFamilyPath : def.HostileFamilyPath);
    if (family == null) return;

    _telegraphScene ??= GD.Load<PackedScene>(TelegraphScenePath);
    if (_telegraphScene?.Instantiate() is not Node3D telegraph) return;

    _view.AddChild(telegraph);
    telegraph.GlobalPosition = origin;
    telegraph.LookAt(origin + direction, Vector3.Up); // -Z is the lanes' forward, matching FillMode.FORWARD
    telegraph.Call("configure",
      family,
      skill.ProjectileCount,
      skill.ProjectileSpacing.ToFloat(),
      skill.ProjectileRadius.ToFloat(),
      range,
      skill.ProjectileSpawnOffset.ToFloat(),
      range / speed, // bars sweep at the speed the bullets travel
      def.LaneHeight);
  }

  // Origin follows the rendered caster when it has a view, so the lanes start at the hero the player is
  // looking at rather than at the sim position the interpolated view trails.
  private static bool TryResolveAim(SkillCastEvent evt, Node3D casterView, out Vector3 origin, out Vector3 direction) {
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
