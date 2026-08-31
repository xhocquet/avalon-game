using System.Runtime.InteropServices;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// A trail being laid behind its caster: armed by a cast, it drops one TrailSegment entity at the
// caster's position every IntervalTicks until SegmentsRemaining runs out, then TrailSystem reaps it.
// The counterpart to SkillCharge - that one waits on the clock to pay out once, this one pays out a
// piece at a time as the caster moves.
//
// The per-segment payload (lifetime, width) is resolved off the asset row at arm time and carried
// here, so every segment a cast drops matches the rank that armed it even if the row is retuned.
// One slot: re-arming replaces.
[KlothoComponent(ComponentIds.TrailEmitter)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct TrailEmitter : IComponent {
  public int SkillAssetId; // Row that armed it; 0 means nothing is emitting
  public int Rank;
  public int SegmentsRemaining;
  public int IntervalTicks; // Ticks between drops
  public int NextDropTick; // Absolute tick the next drop is due
  public int SegmentLifetimeTicks; // How long each dropped segment lingers
  public FP64 Width; // Contact reach of each dropped segment

  public readonly bool IsEmitting => SkillAssetId != 0 && SegmentsRemaining > 0;

  public readonly bool IsDropDue(int tick) {
    return IsEmitting && tick >= NextDropTick;
  }

  public void Clear() {
    SkillAssetId = 0;
    Rank = 0;
    SegmentsRemaining = 0;
    IntervalTicks = 0;
    NextDropTick = 0;
    SegmentLifetimeTicks = 0;
    Width = FP64.Zero;
  }
}
