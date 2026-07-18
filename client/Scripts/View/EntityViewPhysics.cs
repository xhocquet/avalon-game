using Godot;
using xpTURN.Klotho.Godot;

namespace Meesles.Avalon;

internal static class EntityViewPhysics {
  // Dedicated Godot physics layer (20) used ONLY for client-side click selection. Gameplay never queries
  // it — the deterministic sim runs on Klotho, not Godot physics — so pick colliders placed here can never
  // affect the game. InputCapture raycasts against this mask; nothing else touches it.
  public const uint SelectionLayer = 1u << 19;

  private const string SelectionColliderName = "SelectionPickArea";

  public static void DisableGodotCollision(Node node) {
    if (node == null)
      return;

    DisableGodotCollisionRecursive(node);
  }

  // Adds a capsule pick volume on the selection layer only. Must be called AFTER DisableGodotCollision so
  // the pick area is not stripped. The area is a passive raycast target (Monitoring off) — InputCapture
  // finds it via space.IntersectRay with CollideWithAreas, never through overlap or viewport picking.
  //
  // worldRadius/worldHeight are in WORLD metres (the size you actually see in-game). Pass > 0 to pin an
  // exact hitbox; they are converted into the view's local space here since the area is a child of the
  // view and inherits its node scale. When either is <= 0 the size is derived from the visible mesh AABB —
  // unreliable for skinned meshes (their reported bounds don't match the posed silhouette), so prefer
  // explicit overrides for animated units.
  public static void AddSelectionCollider(EntityViewNode view, float worldRadius = 0f, float worldHeight = 0f) {
    if (view == null || view.HasNode(SelectionColliderName))
      return;

    var haveOverrides = worldRadius > 0f && worldHeight > 0f;
    var aabb = new Aabb();
    if (!haveOverrides && (!TryGetLocalAabb(view, out aabb) || aabb.Size == Vector3.Zero))
      return;

    float radius, height;
    Vector3 center;
    if (haveOverrides) {
      // World metres → view-local units (uniform node scale assumed).
      var scale = Mathf.Max(Mathf.Abs(view.GlobalTransform.Basis.Scale.Y), 0.0001f);
      radius = worldRadius / scale;
      height = worldHeight / scale;
      center = new Vector3(0f, height * 0.5f, 0f);
    }
    else {
      radius = Mathf.Max(aabb.Size.X, aabb.Size.Z) * 0.5f;
      height = Mathf.Max(aabb.Size.Y, radius * 2f);
      center = aabb.GetCenter();
    }

    var area = new Area3D {
      Name = SelectionColliderName,
      CollisionLayer = SelectionLayer,
      CollisionMask = 0,
      Monitoring = false,
      Monitorable = true,
      InputRayPickable = false,
      Position = center
    };
    area.AddChild(new CollisionShape3D {
      Shape = new CapsuleShape3D { Radius = radius, Height = height }
    });
    view.AddChild(area);

    if (DebugConfig.DrawSelectionColliders) {
      GD.Print($"[selection] {view.Name}: radius={radius:0.00} height={height:0.00} " +
               $"local-aabb={aabb.Size} view-scale={view.Scale} view-globalscale={view.GlobalTransform.Basis.Scale}");
      area.AddChild(new MeshInstance3D {
        Mesh = new CapsuleMesh { Radius = radius, Height = height },
        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        MaterialOverride = new StandardMaterial3D {
          AlbedoColor = new Color(0.2f, 0.9f, 1f, 0.25f),
          Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
          ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
          CullMode = BaseMaterial3D.CullModeEnum.Disabled
        }
      });
    }
  }

  // Union of every visible mesh's bounds expressed in the view's local space, so the capsule (a child of
  // the view) tracks the entity transform automatically. The SelectionIndicator ring is skipped: it is a
  // flat ground decal that would inflate the silhouette and is not itself a valid click target.
  private static bool TryGetLocalAabb(Node3D root, out Aabb aabb) {
    aabb = default;
    var rootInverse = root.GlobalTransform.AffineInverse();
    var has = false;
    CollectLocalAabb(root, rootInverse, ref aabb, ref has);
    return has;
  }

  private static void CollectLocalAabb(Node node, Transform3D rootInverse, ref Aabb aabb, ref bool has) {
    if (node is SelectionIndicator)
      return;

    if (node is VisualInstance3D visual && visual.Visible) {
      var toRoot = rootInverse * visual.GlobalTransform;
      var box = TransformAabb(toRoot, visual.GetAabb());
      aabb = has ? aabb.Merge(box) : box;
      has = true;
    }

    foreach (var child in node.GetChildren())
      CollectLocalAabb(child, rootInverse, ref aabb, ref has);
  }

  private static Aabb TransformAabb(Transform3D transform, Aabb local) {
    var result = new Aabb(transform * local.GetEndpoint(0), Vector3.Zero);
    for (var i = 1; i < 8; i++)
      result = result.Expand(transform * local.GetEndpoint(i));
    return result;
  }

  private static void DisableGodotCollisionRecursive(Node node) {
    if (node is CollisionObject3D collisionObject) {
      collisionObject.CollisionLayer = 0;
      collisionObject.CollisionMask = 0;
      collisionObject.InputRayPickable = false;
    }

    if (node is CollisionShape3D shape)
      shape.Disabled = true;

    if (node is CollisionPolygon3D polygon)
      polygon.Disabled = true;

    if (node is Area3D area) {
      area.Monitoring = false;
      area.Monitorable = false;
    }

    foreach (var child in node.GetChildren())
      DisableGodotCollisionRecursive(child);
  }
}
