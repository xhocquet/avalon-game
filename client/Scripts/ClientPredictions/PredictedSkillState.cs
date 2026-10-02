using Meesles.Avalon.Sim.Components;

namespace Meesles.Avalon;

// Locally queued upgrades not yet reflected in the sim
public sealed class PredictedSkillState {
  private const int ExpiryTicks = 30; // ~1 second at 30 Hz

  private readonly int[] _baseRank = new int[Skills.MaxSlots]; // Sim rank before outstanding upgrades
  private readonly int[] _asked = new int[Skills.MaxSlots];
  private readonly int[] _outstanding = new int[Skills.MaxSlots];

  private readonly int[] _waited = new int[Skills.MaxSlots]; // HUD syncs since the last queued upgrade

  public int PendingPoints { get; private set; }

  public int OutstandingFor(int slot) {
    return (uint)slot < Skills.MaxSlots ? _outstanding[slot] : 0;
  }

  public int RankFor(int slot, int simRank) { // Sim rank plus unconfirmed upgrades
    return simRank + OutstandingFor(slot);
  }

  public void PredictUpgrade(int slot) { // Call only after queuing the command
    if ((uint)slot >= Skills.MaxSlots) return;

    _asked[slot]++;
    _outstanding[slot]++;
    PendingPoints++;
    _waited[slot] = 0;
  }

  public void Observe(int slot, int simRank) { // Call each HUD sync before painting
    if ((uint)slot >= Skills.MaxSlots) return;

    if (_asked[slot] == 0) {
      _baseRank[slot] = simRank;
      return;
    }

    if (++_waited[slot] >= ExpiryTicks) {
      Retire(slot, simRank);
      return;
    }

    var remaining = _baseRank[slot] + _asked[slot] - simRank;
    if (remaining < 0) remaining = 0;

    ApplyOutstanding(slot, remaining);
    if (remaining == 0)
      Retire(slot, simRank);
  }

  public void Clear() {
    for (var slot = 0; slot < Skills.MaxSlots; slot++) {
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

  private void ApplyOutstanding(int slot, int value) { // Keeps pending points in sync
    PendingPoints += value - _outstanding[slot];
    if (PendingPoints < 0) PendingPoints = 0;
    _outstanding[slot] = value;
  }
}
