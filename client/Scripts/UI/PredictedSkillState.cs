using Meesles.Avalon.Sim.Components;

namespace Meesles.Avalon;

// The skill upgrades the player has asked for but the simulation has not run yet.
//
// Klotho predicts locally, but it stamps local commands at CurrentTick + InputDelayTicks, so the
// predicted frame keeps reporting the old rank for a few ticks after the click - long enough to read
// as a dropped button. This holds the difference between what the player asked for and what the frame
// shows, so the skill bar can paint the new rank on the same frame as the click and the sim's result
// simply takes over when it arrives.
//
// Optimism is bounded two ways: a slot's outstanding count falls as the frame's own rank climbs to
// meet it, and the whole entry expires after ExpiryTicks so a command the sim rejected reverts
// instead of sticking.
public sealed class PredictedSkillState {
  // ~1s at 30Hz. Only reached when a queued command never lands: it has to outlast InputDelayTicks
  // plus any RecommendedExtraDelay escalation, both of which are well under a second.
  private const int ExpiryTicks = 30;

  // Rank the frame reported when this slot's first outstanding upgrade was queued. Asked-for rank is
  // _baseRank + _asked, and the difference against the frame's current rank is what still shows.
  private readonly int[] _baseRank = new int[SkillsComponent.MaxSlots];
  private readonly int[] _asked = new int[SkillsComponent.MaxSlots];
  private readonly int[] _outstanding = new int[SkillsComponent.MaxSlots];

  // Syncs a slot has been waiting with something outstanding. Counted rather than deadlined against
  // frame.Tick so a prediction made before the first sync cannot come out already expired.
  private readonly int[] _waited = new int[SkillsComponent.MaxSlots];

  // Skill points committed to queued commands that the frame has not deducted yet.
  public int PendingPoints { get; private set; }

  public int OutstandingFor(int slot) {
    return (uint)slot < SkillsComponent.MaxSlots ? _outstanding[slot] : 0;
  }

  // Rank to display for a slot: what the frame says, plus whatever the player asked for that it has
  // not caught up to.
  public int RankFor(int slot, int simRank) {
    return simRank + OutstandingFor(slot);
  }

  // Called when a command is actually queued for sending, never on the click alone - an upgrade the
  // client itself refused must not move the display.
  public void PredictUpgrade(int slot) {
    if ((uint)slot >= SkillsComponent.MaxSlots) return;

    _asked[slot]++;
    _outstanding[slot]++;
    PendingPoints++;
    _waited[slot] = 0;
  }

  // Called once per slot per HUD sync with the frame's own rank, before the slot is painted.
  public void Observe(int slot, int simRank) {
    if ((uint)slot >= SkillsComponent.MaxSlots) return;

    if (_asked[slot] == 0) {
      // Nothing in flight: the frame is the truth, and its rank is the base the next click builds on.
      _baseRank[slot] = simRank;
      return;
    }

    if (++_waited[slot] >= ExpiryTicks) {
      Retire(slot, simRank);
      return;
    }

    // How much of what was asked for the frame still has not shown. Falls to zero as the sim catches
    // up, one rank per landed command, and cannot go negative if the frame overshoots.
    var remaining = _baseRank[slot] + _asked[slot] - simRank;
    if (remaining < 0) remaining = 0;

    ApplyOutstanding(slot, remaining);
    if (remaining == 0)
      Retire(slot, simRank);
  }

  public void Clear() {
    for (var slot = 0; slot < SkillsComponent.MaxSlots; slot++) {
      _baseRank[slot] = 0;
      _asked[slot] = 0;
      _outstanding[slot] = 0;
      _waited[slot] = 0;
    }

    PendingPoints = 0;
  }

  private void Retire(int slot, int simRank) {
    ApplyOutstanding(slot, 0);
    _asked[slot] = 0;
    _baseRank[slot] = simRank;
    _waited[slot] = 0;
  }

  // PendingPoints tracks the sum of the outstanding counts, so it is maintained through the same
  // door rather than recomputed.
  private void ApplyOutstanding(int slot, int value) {
    PendingPoints += value - _outstanding[slot];
    if (PendingPoints < 0) PendingPoints = 0;
    _outstanding[slot] = value;
  }
}
