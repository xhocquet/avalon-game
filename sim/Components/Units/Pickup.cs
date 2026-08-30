using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Components;

// Neutral, collectable resource pickup. Like Oasis, carries no Team/Health/UnitId so it stays
// invisible to TargetAcquisitionSystem/DamageSystem; PickupSystem collects it by proximity only.
[KlothoComponent(ComponentIds.Pickup)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct Pickup : IComponent {
  public int PickupId;
  public int Amount;
  public int TypeAssetId; // PickupTypeAsset id; fixed at spawn, never changes
}
