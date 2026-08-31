using System.Runtime.InteropServices;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// One circle of a laid trail, one entity per drop, advanced and contact-checked by TrailSystem. Like
// a Projectile it carries no Team/Health/UnitIdentity - those are what would make it a target - so the
// laying team rides on the component as a plain int and the segment outlives a caster who dies.
//
// Contact is a per-tick proximity test against TrailSegment.Width, not physics. What a caught unit
// gets comes off the skill row's buff block, re-read at contact time and keyed to the row so standing
// in the trail refreshes rather than stacks.
[KlothoComponent(ComponentIds.TrailSegment)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct TrailSegment : IComponent {
  public int SegmentId;
  public int SourceUnitId; // Caster, for the buff's source attribution
  public int TeamId; // Stamped at drop; the segment's allegiance is fixed even if the caster's changes
  public int SkillAssetId;
  public int Rank;
  public int ExpiryTick;
  public FP64 Width; // Contact reach, widened by the caught unit's own body
}
