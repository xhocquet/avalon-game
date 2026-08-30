using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// Per-hero skill tree state: the unspent points ExperienceSystem grants, which SkillAsset sits in each
// slot, how far each is ranked up, and how long until it can be cast again. Stored as fixed buffers
// (not lists) because components must be unmanaged, blittable structs - the whole struct is
// snapshotted via a raw heap memcpy for rollback, and the generated codec walks the buffers for
// hashing and serialization. See Inventory for the same pattern.
// Size: 4 ints + 3 * MaxSlots * 4 = 52B, inside the 128-byte component ceiling.
//
// SkillAssetIds is copied off HeroAsset at spawn rather than looked up per access, so the cast path
// never has to reach the asset registry to find out which skills a hero owns.
[KlothoComponent(ComponentIds.Skills)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public unsafe partial struct Skills : IComponent {
  public const int MaxSlots = 4;

  public int SkillPoints;
  public fixed int SkillAssetIds[MaxSlots];
  public fixed int Ranks[MaxSlots];
  public fixed int CooldownRemainingTicks[MaxSlots];

  // Every accessor below is unchecked on the slot index because it indexes a fixed buffer directly.
  // CommandValidation gates wire input through this before any of them run.
  public static bool IsValidSlot(int slot) {
    return slot >= 0 && slot < MaxSlots;
  }

  public readonly int GetSkillAssetId(int slot) {
    return SkillAssetIds[slot];
  }

  public readonly int GetRank(int slot) {
    return Ranks[slot];
  }

  public readonly int GetCooldownRemainingTicks(int slot) {
    return CooldownRemainingTicks[slot];
  }

  public readonly bool IsReady(int slot) {
    return Ranks[slot] > 0 && CooldownRemainingTicks[slot] == 0;
  }

  public void SetSkillAssetId(int slot, int skillAssetId) {
    SkillAssetIds[slot] = skillAssetId;
  }

  // Spend one point to raise a slot a rank. Returns false (no-op) when the hero is out of points or
  // the slot is already capped, letting the caller reject without half-applying the upgrade.
  public bool TrySpendPoint(int slot, int maxRank) {
    if (SkillPoints <= 0 || Ranks[slot] >= maxRank)
      return false;

    SkillPoints--;
    Ranks[slot]++;
    return true;
  }

  public void StartCooldown(int slot, int ticks) {
    CooldownRemainingTicks[slot] = ticks;
  }

  // Called once per tick by TimedEffectSystem. A skill cast on tick N loses one tick here on that same
  // tick, because commands are delivered before the Update phase runs - the same one-tick behaviour
  // attack cooldowns have, identical on both peers. It is not an off-by-one.
  public void TickCooldowns() {
    for (var i = 0; i < MaxSlots; i++)
      if (CooldownRemainingTicks[i] > 0)
        CooldownRemainingTicks[i]--;
  }
}
