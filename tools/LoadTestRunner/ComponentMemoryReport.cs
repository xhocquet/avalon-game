using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.ECS;

internal static class ComponentMemoryReport {
  public static void Write(Frame frame, string output = null) {
    var rows = ComponentStorageRegistry.RegisteredTypes.Select(type => {
      var id = ComponentStorageRegistry.GetTypeId(type);
      var layout = ComponentStorageRegistry.GetLayout(id);
      return (Type: type, Group: GroupFor(type, id), Layout: layout, Live: frame.GetLiveCount(id));
    }).OrderByDescending(row => row.Layout.TotalSize).ThenBy(row => row.Type.FullName).ToArray();
    var groups = rows.GroupBy(row => row.Group)
      .OrderByDescending(group => group.Sum(row => row.Layout.TotalSize)).ThenBy(group => group.Key).ToArray();
    var heapBytes = ComponentStorageRegistry.GetHeapSize(frame.MaxEntities);

    Console.WriteLine($"\nComponent memory at tick {frame.Tick}; max entities {frame.MaxEntities}");
    long emptyBytes = 0;
    foreach (var group in groups) {
      var reserved = group.Sum(row => row.Layout.TotalSize);
      Console.WriteLine($"\n{group.Key}: {reserved:N0} bytes ({100.0 * reserved / heapBytes:F1}% of heap)");
      Console.WriteLine("component                       live  slots  bytes/slot   reserved B  empty slots B");
      foreach (var row in group) {
        var layout = row.Layout;
        var empty = (long)(layout.SlotCapacity - row.Live) * (layout.ComponentSize + sizeof(int));
        emptyBytes += empty;
        Console.WriteLine($"{row.Type.Name,-30} {row.Live,5} {layout.SlotCapacity,6} {layout.ComponentSize,11} {layout.TotalSize,12} {empty,14}");
      }
    }
    Console.WriteLine($"Heap per frame: {heapBytes:N0} bytes");
    Console.WriteLine($"Empty component/dense slots: {emptyBytes:N0} bytes (excludes sparse indices and headers)");
    Console.WriteLine("Reserved bytes include payload and indexing. Empty slots are headroom, not automatically safe to remove.");
    if (string.IsNullOrWhiteSpace(output))
      return;
    var html = new StringBuilder("<!doctype html><html lang=\"en\"><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>Avalon component memory</title>");
    html.Append("<style>:root{color-scheme:dark;font:15px/1.5 system-ui;background:#10151e;color:#e4eaf2}body{max-width:1100px;margin:40px auto;padding:0 20px}table{width:100%;border-collapse:collapse;font-variant-numeric:tabular-nums}td,th{padding:9px;border-bottom:1px solid #2d3b50;text-align:right}td:first-child,th:first-child{text-align:left}th{position:sticky;top:0;background:#192231}p{color:#abb9cc}.capped{background:#1b3040}.metric{font-size:28px;font-weight:600;color:#fff}</style>");
    html.Append($"<h1>Component memory</h1><p>Simulation tick {frame.Tick} · max entities {frame.MaxEntities} · player capacity {Meesles.Avalon.Sim.SimulationCapacity.PlayerSlots} · oasis capacity {Meesles.Avalon.Sim.SimulationCapacity.OasisSlots}</p>");
    html.Append($"<p class=\"metric\">{heapBytes:N0} bytes per frame</p><p>Grouped by responsibility, with the largest groups and components first. Each component appears once. These are reporting categories, not separate memory pools. Highlighted rows have an explicit non-singleton cap below the entity limit. This report reflects the current build; earlier CPU/GPU captures retain their original layout.</p>");
    html.Append("<h2>Groups</h2><table><thead><tr><th>Group</th><th>Components</th><th>Reserved bytes</th><th>Heap share</th></tr></thead><tbody>");
    for (var i = 0; i < groups.Length; i++) {
      var group = groups[i];
      var reserved = group.Sum(row => row.Layout.TotalSize);
      html.Append($"<tr><td><a href=\"#group-{i}\">{WebUtility.HtmlEncode(group.Key)}</a></td><td>{group.Count()}</td><td>{reserved:N0}</td><td>{100.0 * reserved / heapBytes:F1}%</td></tr>");
    }
    html.Append("</tbody></table><h2>Components by group</h2>");
    for (var i = 0; i < groups.Length; i++) {
      var group = groups[i];
      var reserved = group.Sum(row => row.Layout.TotalSize);
      html.Append($"<details open id=\"group-{i}\"><summary style=\"font-size:18px;font-weight:600;padding:20px 0 10px;cursor:pointer\">{WebUtility.HtmlEncode(group.Key)} · {reserved:N0} bytes</summary>");
      html.Append("<table><thead><tr><th>Component</th><th>Live</th><th>Slots</th><th>Bytes/component</th><th>Reserved bytes</th><th>Empty slot bytes</th></tr></thead><tbody>");
      foreach (var row in group) {
        var layout = row.Layout;
        var capped = layout.SlotCapacity > 0 && layout.SlotCapacity < frame.MaxEntities &&
                     !ComponentStorageRegistry.IsSingleton(layout.TypeId);
        var empty = (long)(layout.SlotCapacity - row.Live) * (layout.ComponentSize + sizeof(int));
        html.Append($"<tr{(capped ? " class=\"capped\"" : "")}><td>{WebUtility.HtmlEncode(row.Type.Name)}</td><td>{row.Live}</td><td>{layout.SlotCapacity}</td><td>{layout.ComponentSize}</td><td>{layout.TotalSize:N0}</td><td>{empty:N0}</td></tr>");
      }
      html.Append("</tbody></table></details>");
    }
    html.Append("<p>Reserved bytes include component data, dense indices, sparse indices and headers. Empty slot bytes include component data and dense indices only; they are future headroom, not automatically removable waste. Live counts describe this fixture at this tick, not every possible match.</p></html>");
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
    File.WriteAllText(output, html.ToString());
    Console.WriteLine($"Memory report: {Path.GetFullPath(output)}");
  }

  private static string GroupFor(Type type, int id) {
    if (type.Namespace?.StartsWith("xpTURN.Klotho", StringComparison.Ordinal) == true)
      return type.Name switch {
        "NavAgentComponent" or "MovementComponent" or "VelocityComponent" or
          "ErrorCorrectionTargetComponent" => "Navigation and movement",
        "PhysicsBodyComponent" or "TransformComponent" => "Physics and transforms",
        "HealthComponent" or "CombatComponent" => "Stats and combat",
        "OwnerComponent" => "Players and progression",
        "MatchEndStateComponent" or "RandomSeedComponent" or "SessionParticipantComponent" => "Match state and identifiers",
        "HFSMComponent" => "Framework behavior",
        _ => "Other framework components"
      };
    return id switch {
      ComponentIds.UnitMoveTarget or ComponentIds.NavSnapTracker or ComponentIds.MinionSettleTracker => "Navigation and movement",
      ComponentIds.Stats or ComponentIds.BaseStats or ComponentIds.Health or ComponentIds.Combat or
        ComponentIds.AttackTargetUnitId or ComponentIds.AttackProc or ComponentIds.AttackBurst or
        ComponentIds.Buffs or ComponentIds.BuffChanges or ComponentIds.DamageOverTime or
        ComponentIds.Snare or ComponentIds.Silence => "Stats and combat",
      ComponentIds.Skills or ComponentIds.SkillChannel or ComponentIds.SkillCharge or ComponentIds.SkillDash or
        ComponentIds.SkillStockpiles or ComponentIds.Projectile or ComponentIds.TrailEmitter or
        ComponentIds.TrailSegment => "Skills and effects",
      ComponentIds.Hero or ComponentIds.Player or ComponentIds.PlayerFaction or ComponentIds.Inventory or
        ComponentIds.Resources or ComponentIds.Experience or ComponentIds.Respawns or
        ComponentIds.PendingRespawn => "Players and progression",
      ComponentIds.Oasis or ComponentIds.OasisEjectPending or ComponentIds.OasisResourceLanding or
        ComponentIds.Pickup or ComponentIds.Crystal or ComponentIds.Turret or ComponentIds.SpawnPoint => "World and pickups",
      ComponentIds.UnitIdentity or ComponentIds.Team or ComponentIds.Faction or ComponentIds.Minion or
        ComponentIds.Controllable or ComponentIds.UnitVisibility => "Units and visibility",
      ComponentIds.UnitIdCounter or ComponentIds.PickupIdCounter or ComponentIds.ProjectileIdCounter or
        ComponentIds.TrailSegmentIdCounter or ComponentIds.AttackHitIdCounter or ComponentIds.MatchOutcome or
        ComponentIds.MatchSetupState or ComponentIds.CheatState => "Match state and identifiers",
      _ => "Other game components"
    };
  }
}
