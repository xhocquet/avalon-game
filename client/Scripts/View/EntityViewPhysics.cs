using Godot;

namespace Meesles.Avalon {
  internal static class EntityViewPhysics {
    public static void DisableGodotCollision(Node node) {
      if (node == null)
        return;

      DisableGodotCollisionRecursive(node);
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

      foreach (Node child in node.GetChildren())
        DisableGodotCollisionRecursive(child);
    }
  }
}
